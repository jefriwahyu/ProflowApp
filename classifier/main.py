from fastapi import FastAPI
from pydantic import BaseModel
import joblib
import os
from Sastrawi.Stemmer.StemmerFactory import StemmerFactory

app = FastAPI()

MODEL_DIR = os.path.join(os.path.dirname(__file__), "models")
model = joblib.load(os.path.join(MODEL_DIR, "model.pkl"))

stemmer = StemmerFactory().create_stemmer()

import pandas as pd

class PengajuanRequest(BaseModel):
    category: str
    keterangan: str

@app.post("/classify")
def classify_urgency(req: PengajuanRequest):
    keterangan_stemmed = stemmer.stem(req.keterangan.lower())
    input_df = pd.DataFrame([{
        'Category': req.category,
        'Keterangan_stemmed': keterangan_stemmed
    }])
    urgency_level = model.predict(input_df)[0]

    return {
        "urgency_level": urgency_level
    }