import joblib
import os

MODEL_DIR = os.path.join(os.path.dirname(__file__), "models")

model = joblib.load(os.path.join(MODEL_DIR, "nb_model.pkl"))
vectorizer = joblib.load(os.path.join(MODEL_DIR, "count_vectorizer.pkl"))

print("Model berhasil di-load")
print("Classes:", model.classes_)
print("Jumlah vocabulary:", len(vectorizer.vocabulary_))

# Test prediksi cepat
test_text = "ac ruang rapat bocor tetes air"  # sudah dalam bentuk stemmed, contoh saja
vec = vectorizer.transform([test_text])
print("Prediksi:", model.predict(vec))