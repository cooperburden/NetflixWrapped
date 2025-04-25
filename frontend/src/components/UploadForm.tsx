import React, { useState } from "react";

const UploadForm: React.FC = () => {
  const [file, setFile] = useState<File | null>(null);
  const [response, setResponse] = useState<any>(null);
  const [chunkSize, setChunkSize] = useState<number>(25);
  const [offset, setOffset] = useState<number>(0);
  const [loading, setLoading] = useState<boolean>(false);
  const [directorQuery, setDirectorQuery] = useState("");






  const handleFileChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    if (e.target.files?.length) {
      setFile(e.target.files[0]);
    }
  };

  const handleUpload = async () => {
    if (!file) return;
  
    setLoading(true); // 🟡 Start loading
  
    const formData = new FormData();
    formData.append("file", file);
    formData.append("chunkSize", chunkSize.toString());
    formData.append("offset", offset.toString());

  
    const res = await fetch("http://localhost:5014/api/upload/csv", {
      method: "POST",
      body: formData,
    });
  
    const data = await res.json();
  
    setResponse((prev: any) => ({
      ...data,
      enriched: [...(prev?.enriched || []), ...data.enriched],
    }));
  
    setOffset(offset + chunkSize);
    setLoading(false); // ✅ Stop loading
  };
  
  

  return (
    <div>
      <h2>Upload your Netflix Viewing CSV</h2>
      <input type="file" accept=".csv" onChange={handleFileChange} />
      <button onClick={handleUpload} disabled={!file}>
        Upload
      </button>

{loading && <p style={{ fontStyle: "italic" }}>⏳ Loading more titles...</p>}

      {response && (
        
  <div style={{ marginTop: "1rem" }}>
    <h3>{response.message}</h3>
    <p>Total Titles Enriched: {response.totalTitles}</p>
    <div style={{ display: "flex", flexWrap: "wrap", gap: "1rem" }}>
    <div style={{ marginTop: "1rem" }}>
  <label htmlFor="director-search"><strong>Filter by Director:</strong></label>
  <input
    type="text"
    id="director-search"
    placeholder="e.g. Nolan, Spielberg..."
    value={directorQuery}
    onChange={(e) => setDirectorQuery(e.target.value)}
    style={{
      padding: "6px 10px",
      marginLeft: "10px",
      marginBottom: "1rem",
      border: "1px solid #ccc",
      borderRadius: "4px",
      width: "250px"
    }}
  />
</div>





{response.enriched
  .filter((movie: any) =>
    !directorQuery ||
    movie.directors?.some((dir: string) =>
      dir.toLowerCase().includes(directorQuery.toLowerCase())
    )
  )
  .map((movie: any, index: number) => (
        <div
        key={index}
        style={{
          background: "#fff",
          borderRadius: "12px",
          padding: "1rem",
          width: "250px",
          boxShadow: "0 4px 12px rgba(0,0,0,0.1)",
          transition: "transform 0.2s ease-in-out",
          transform: "scale(1)",
          cursor: "pointer",
        }}
        onMouseEnter={(e) => (e.currentTarget.style.transform = "scale(1.03)")}
        onMouseLeave={(e) => (e.currentTarget.style.transform = "scale(1)")}
      >
          {movie.poster && (
            <img
              src={movie.poster}
              alt={movie.title}
              style={{ width: "100%", borderRadius: "4px" }}
            />
          )}
          <h4>{movie.title}</h4>
          <p>
            <strong>Genres:</strong>{" "}
            {movie.genres?.length ? movie.genres.join(", ") : "N/A"}
          </p>
          <p>
            <strong>Director:</strong>{" "}
            {movie.directors?.length ? movie.directors.join(", ") : "N/A"}
          </p>
          <p>
            <strong>Actors:</strong>{" "}
            {movie.actors?.length ? movie.actors.join(", ") : "N/A"}
          </p>
          <p>
            <strong>Runtime:</strong>{" "}
            {movie.runtime ? `${movie.runtime} min` : "N/A"}
          </p>
        </div>
      ))}
    </div>
  </div>
)}

<button onClick={handleUpload} style={{ marginTop: "1rem" }}>
  Load More
</button>


    </div>
  );
};

export default UploadForm;
