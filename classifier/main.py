from fastapi import FastAPI
from pydantic import BaseModel
import re
import os
import joblib
from Sastrawi.StopWordRemover.StopWordRemoverFactory import StopWordRemoverFactory
from Sastrawi.Stemmer.StemmerFactory import StemmerFactory

app = FastAPI()

MODEL_DIR = os.path.join(os.path.dirname(__file__), "models")

model = joblib.load(os.path.join(MODEL_DIR, "nb_model.pkl"))
vectorizer = joblib.load(os.path.join(MODEL_DIR, "count_vectorizer.pkl"))

stopword_remover = StopWordRemoverFactory().create_stop_word_remover()
stemmer = StemmerFactory().create_stemmer()

THRESHOLD_AUTO = 0.80
THRESHOLD_REVIEW = 0.60

def preprocess(text: str) -> str:
    text = str(text).lower()
    text = re.sub(r"[^a-z\s]", " ", text)
    text = re.sub(r"\s+", " ", text).strip()
    text = stopword_remover.remove(text)
    text = stemmer.stem(text)
    return text

class PengajuanRequest(BaseModel):
    keterangan: str

@app.post("/classify")
def classify_urgency(req: PengajuanRequest):
    clean = preprocess(req.keterangan)
    vec = vectorizer.transform([clean])
    
    pred = model.predict(vec)[0]
    proba = dict(zip(model.classes_, model.predict_proba(vec)[0]))
    confidence = max(proba.values())

    if confidence >= THRESHOLD_AUTO:
        status = "AUTO_APPROVE"
    elif confidence >= THRESHOLD_REVIEW:
        status = "REKOMENDASI"
    else:
        status = "MANUAL_REVIEW"

    return {
        "urgency_level": pred,
        "confidence": round(float(confidence), 4),
        "status": status
    }