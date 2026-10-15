from fastapi import FastAPI
from pydantic import BaseModel, Field
from sklearn.feature_extraction.text import TfidfVectorizer
from sklearn.linear_model import LogisticRegression
from sklearn.pipeline import Pipeline

from train_data import TRAINING_DATA

app = FastAPI(title="Grievance AI Service")

texts = [item[0] for item in TRAINING_DATA]
labels = [item[1] for item in TRAINING_DATA]

model = Pipeline([
    ("tfidf", TfidfVectorizer(
        ngram_range=(1, 2),
        lowercase=True
    )),
    ("classifier", LogisticRegression(
        max_iter=1000,
        class_weight="balanced"
    ))
])

model.fit(texts, labels)


class ComplaintInput(BaseModel):
    text: str = Field(min_length=5, max_length=6000)


def predict_priority(text: str) -> str:
    text = text.lower()

    if any(word in text for word in [
        "fire", "electrocution", "life threatening",
        "gas leak", "trapped"
    ]):
        return "Critical"

    if any(word in text for word in [
        "dangerous", "accident", "sparking",
        "no water", "sewage", "overflowing"
    ]):
        return "High"

    return "Medium"


@app.get("/health")
def health():
    return {
        "status": "healthy",
        "model": "TF-IDF + Logistic Regression"
    }


@app.post("/predict")
def predict(data: ComplaintInput):
    probabilities = model.predict_proba([data.text])[0]
    best_index = int(probabilities.argmax())

    category = str(model.classes_[best_index])

    return {
        "category": category,
        "confidence": round(
            float(probabilities[best_index]), 4
        ),
        "priority": predict_priority(data.text),
        "model": "tfidf-logistic-regression",
        "review_required": True
    }