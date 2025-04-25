from flask import Flask, request, jsonify
import requests
from flask_cors import CORS

app = Flask(__name__)
CORS(app)

# Replace this with your actual TMDb API key
TMDB_API_KEY = "327891dabaa8ec8287a899d9880b927a"

def try_alternate_titles(title):
    parts = title.split(":")
    fallbacks = []

    # Full title first
    fallbacks.append(title.strip())

    # Then progressively shorter prefixes
    if len(parts) > 1:
        for i in range(len(parts) - 1, 0, -1):
            fallback = ":".join(parts[:i]).strip()
            fallbacks.append(fallback)

    return fallbacks

def search_tmdb(title):
    for alt_title in try_alternate_titles(title):
        search_url = "https://api.themoviedb.org/3/search/multi"
        search_params = {
            "api_key": TMDB_API_KEY,
            "query": alt_title
        }

        response = requests.get(search_url, params=search_params)
        data = response.json()
        results = data.get("results", [])

        for result in results:
            tmdb_title = result.get("title") or result.get("name")
            if tmdb_title and tmdb_title.lower() in title.lower():
                return result

        # Fallback to first match if no good title match found
        if results:
            return results[0]

    return None


def enrich_title(original_title):
    item = search_tmdb(original_title)
    if not item:
        return {"title": original_title, "found": False}

    tmdb_id = item["id"]
    media_type = item["media_type"]

    details_url = f"https://api.themoviedb.org/3/{media_type}/{tmdb_id}"
    credits_url = (
        f"https://api.themoviedb.org/3/tv/{tmdb_id}/aggregate_credits"
        if media_type == "tv"
        else f"https://api.themoviedb.org/3/{media_type}/{tmdb_id}/credits"
    )
    params = {"api_key": TMDB_API_KEY}

    details = requests.get(details_url, params=params).json()
    credits = requests.get(credits_url, params=params).json()

    # ✅ Smart director fallback logic
    if media_type == "tv":
        crew = credits.get("crew", [])
        directors = [p["name"] for p in crew if p.get("job") == "Director"]

        if not directors:
            directors = [p["name"] for p in crew if p.get("job") == "Creator"]

        if not directors:
            directors = [p["name"] for p in details.get("created_by", [])]
    else:
        directors = [p["name"] for p in credits.get("crew", []) if p.get("job") == "Director"]

    # 🎭 Top 5 actors
    cast_source = credits.get("cast", [])
    top_actors = [p["name"] for p in cast_source[:5]]

    genres = [g["name"] for g in details.get("genres", [])]
    runtime = details.get("runtime") or (details.get("episode_run_time") or [None])[0]
    poster = details.get("poster_path")

    return {
        "title": original_title,
        "found": True,
        "tmdb_id": tmdb_id,
        "media_type": media_type,
        "runtime": runtime,
        "genres": genres,
        "directors": directors,
        "actors": top_actors,
        "poster": f"https://image.tmdb.org/t/p/w500{poster}" if poster else None
    }


@app.route("/enrich", methods=["POST"])
def enrich():
    titles = request.json.get("titles", [])
    enriched = [enrich_title(t) for t in titles]
    return jsonify(enriched)

if __name__ == "__main__":
    app.run(host="0.0.0.0", port=5001)
