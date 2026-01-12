curl -X POST http://localhost:5000/jobs \
  -H "Content-Type: application/json" \
  -d '{
    "type": "http-ping",
    "payload": {
      "url": "https://example.com"
    }
  }'
