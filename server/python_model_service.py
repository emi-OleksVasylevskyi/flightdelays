#!/usr/bin/env python3
"""
Python service to handle actual machine learning model predictions
for the Flight Delay API
"""

import joblib
import pandas as pd
import numpy as np
from flask import Flask, request, jsonify
import logging
from typing import Dict, Any

app = Flask(__name__)
logging.basicConfig(level=logging.INFO)

# Global model storage
model_data = None

def load_model():
    """Load the trained model and data"""
    global model_data
    try:
        model_path = '/workspaces/flightdelays/data/delay_model_with_pair_rates.joblib'
        model_data = joblib.load(model_path)
        
        app.logger.info(f"Model loaded successfully")
        app.logger.info(f"Feature columns: {model_data['feature_columns']}")
        app.logger.info(f"Global delay probability: {model_data['global_delay_prob']:.4f}")
        app.logger.info(f"Pair delay rates shape: {model_data['pair_delay_rates'].shape}")
        
    except Exception as e:
        app.logger.error(f"Failed to load model: {e}")
        raise

def predict_delay(request_data: Dict[str, Any]) -> Dict[str, Any]:
    """
    Predict flight delay using the trained model
    
    Args:
        request_data: Dictionary with keys: Month, DayOfWeek, Carrier, OriginAirportID, DestAirportID, CRSDepTime
    
    Returns:
        Dictionary with prediction results
    """
    try:
        # Extract departure hour from CRSDepTime (e.g., 800 -> 8)
        dep_hour = request_data.get('CRSDepTime', 800) // 100
        
        # Prepare features for the logistic model
        features = {
            'Month': request_data.get('Month', 1),
            'DayOfWeek': request_data.get('DayOfWeek', 1),
            'Carrier': request_data.get('Carrier', 'AA'),
            'OriginAirportID': request_data.get('OriginAirportID', 10397),
            'DestAirportID': request_data.get('DestAirportID', 10721),
            'DepHour': dep_hour
        }
        
        # Create DataFrame for prediction
        feature_df = pd.DataFrame([features])
        
        # Get prediction from logistic model
        logistic_model = model_data['logistic_model']
        logistic_prob = logistic_model.predict_proba(feature_df)[0][1]  # Probability of delay (class 1)
        
        # Get historical pair delay rate
        pair_rates = model_data['pair_delay_rates']
        pair_key = (
            features['OriginAirportID'],
            features['DestAirportID'], 
            features['DayOfWeek']
        )
        
        pair_row = pair_rates[
            (pair_rates['OriginAirportID'] == pair_key[0]) &
            (pair_rates['DestAirportID'] == pair_key[1]) &
            (pair_rates['DayOfWeek'] == pair_key[2])
        ]
        
        if not pair_row.empty:
            historical_prob = pair_row['smoothed_delay_prob'].iloc[0]
            historical_events = pair_row['delay_events'].iloc[0]
            historical_total = pair_row['n'].iloc[0]
            has_historical_data = True
        else:
            historical_prob = model_data['global_delay_prob']
            historical_events = 0
            historical_total = 0
            has_historical_data = False
        
        # Combine predictions (weighted average)
        # Give more weight to logistic model, but incorporate historical data
        if has_historical_data and historical_total >= 5:
            combined_prob = 0.7 * logistic_prob + 0.3 * historical_prob
            confidence_base = 0.85
        elif has_historical_data:
            combined_prob = 0.8 * logistic_prob + 0.2 * historical_prob
            confidence_base = 0.75
        else:
            combined_prob = 0.9 * logistic_prob + 0.1 * historical_prob
            confidence_base = 0.65
        
        # Calculate confidence based on prediction agreement and data availability
        prediction_agreement = 1.0 - abs(logistic_prob - historical_prob)
        confidence = min(0.95, confidence_base + 0.15 * prediction_agreement)
        
        return {
            'DelayProbability': round(combined_prob, 4),
            'ConfidencePercent': round(confidence * 100, 1),
            'LogisticProbability': round(logistic_prob, 4),
            'HistoricalPairProbability': round(historical_prob, 4),
            'PredictionMethod': 'Trained Model',
            'HasHistoricalData': has_historical_data,
            'HistoricalEvents': int(historical_events) if has_historical_data else None,
            'HistoricalTotal': int(historical_total) if has_historical_data else None
        }
        
    except Exception as e:
        app.logger.error(f"Prediction error: {e}")
        raise

@app.route('/health', methods=['GET'])
def health_check():
    """Health check endpoint"""
    return jsonify({
        'status': 'healthy',
        'model_loaded': model_data is not None,
        'timestamp': pd.Timestamp.now().isoformat()
    })

@app.route('/predict', methods=['POST'])
def predict():
    """Predict flight delay endpoint"""
    try:
        if model_data is None:
            return jsonify({'error': 'Model not loaded'}), 500
        
        request_json = request.get_json()
        if not request_json:
            return jsonify({'error': 'No JSON data provided'}), 400
        
        # Validate required fields
        required_fields = ['DayOfWeek', 'OriginAirportID', 'DestAirportID']
        for field in required_fields:
            if field not in request_json:
                return jsonify({'error': f'Missing required field: {field}'}), 400
        
        prediction = predict_delay(request_json)
        return jsonify(prediction)
        
    except Exception as e:
        app.logger.error(f"Prediction endpoint error: {e}")
        return jsonify({'error': str(e)}), 500

if __name__ == '__main__':
    load_model()
    app.run(host='0.0.0.0', port=5108, debug=False)