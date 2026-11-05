import React from "react";
import { createRoot } from "react-dom/client";
import "../style.css";

function EightBall() {
  const [answer, setAnswer] = React.useState("");
  const [asking, setAsking] = React.useState(false);
  const [error, setError] = React.useState("");

  async function ask() {
    setAsking(true);
    setError("");
    try {
      const response = await fetch("/api/answer");
      if (!response.ok) {
        throw new Error(`The API returned ${response.status}.`);
      }
      const result = await response.json();
      setAnswer(result.answer);
    } catch (requestError) {
      setError(requestError.message || "The answer service is unavailable.");
    } finally {
      setAsking(false);
    }
  }

  return (
    <main className="shell">
      <header className="brand">
        <span className="brand-mark" aria-hidden="true">✦</span>
        <span>THE ORACLE</span>
      </header>
      <section className="card" aria-labelledby="title">
        <h1 id="title">Ask the<br /><em>eight ball.</em></h1>
        <p className="intro">Hold a question in your mind, then let the universe weigh in.</p>
        <div className={`orb ${answer ? "orb-answered" : ""}`} aria-live="polite">
          <div className="orb-inner">
            <span className="orb-star" aria-hidden="true">✳</span>
            <span className="answer">{asking ? "Listening..." : answer || "YOUR ANSWER AWAITS"}</span>
          </div>
        </div>
        <button type="button" onClick={ask} disabled={asking}>
          {asking ? "Consulting..." : "Ask the universe"}
          <span aria-hidden="true">↗</span>
        </button>
        <p className="error" role="alert">{error}</p>
      </section>
      <footer>THE FUTURE IS A LITTLE LESS MYSTERIOUS</footer>
    </main>
  );
}

createRoot(document.getElementById("root")).render(
  <React.StrictMode>
    <EightBall />
  </React.StrictMode>
);
