#!/usr/bin/env python3
import joblib
import pandas as pd
import numpy as np

# Load the model
model_path = '/workspaces/flightdelays/data/delay_model_with_pair_rates.joblib'
model_data = joblib.load(model_path)

print("Model data structure:")
print(f"Type: {type(model_data)}")

if isinstance(model_data, dict):
    print("Keys:", model_data.keys())
    for key, value in model_data.items():
        print(f"  {key}: {type(value)}")
        if hasattr(value, 'shape'):
            print(f"    Shape: {value.shape}")
        elif hasattr(value, '__len__') and not isinstance(value, str):
            print(f"    Length: {len(value)}")

# Try to load pair rates data
pair_rates_path = '/workspaces/flightdelays/data/pair_delay_rates.csv'
try:
    pair_rates = pd.read_csv(pair_rates_path)
    print(f"\nPair rates CSV shape: {pair_rates.shape}")
    print("Columns:", pair_rates.columns.tolist())
    print("First few rows:")
    print(pair_rates.head())
except Exception as e:
    print(f"Error loading pair rates: {e}")