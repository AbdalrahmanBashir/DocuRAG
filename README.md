# RAG (Retrieval-Augmented Generation) System

A .NET 8.0-based RAG system that leverages Ollama for document processing, embedding generation, and semantic search capabilities. The system implements a robust architecture with caching, rate limiting, and health monitoring features.

## Features

### Core Functionality
- PDF document processing and text extraction
- Semantic embedding generation using Ollama's `nomic-embed-text` model
- Semantic similarity search with configurable thresholds
- Two-tier caching system:
  - In-memory cache (24-hour duration)
  - File-based persistent cache with JSON storage

## Prerequisites

- .NET 8.0 SDK
- Ollama with `nomic-embed-text` model installed
- Sufficient storage space for document and cache storage
- (Optional) OpenTelemetry collector for tracing

## Installation

1. Clone the repository:
```bash
git clone https://github.com/AbdalrahmanBashir/DocuRAG.git
cd Rag
```

2. Install the required Ollama model:
```bash
ollama pull nomic-embed-text
```

3. Configure the application in `appsettings.json`:
```json
{
    "Storage": {
        "CachePath": "cache",
        "DataPath": "data",
        "ReadOnlyMode": false
    },
    "OllamaSettings": {
        "OllamaEndpoint": "http://localhost:11434",
        "ModelName": "nomic-embed-text",
        "MaxTextLength": 8000,
        "MinTextLength": 10,
        "MaxRetries": 3,
        "RetryDelayMs": 2000,
        "ChunkSize": 1000,
        "ChunkOverlap": 100,
        "SimilarityThreshold": 0.7,
        "MaxResults": 3
    }
}
```

4. Build and run the application:
```bash
dotnet build
dotnet run --project Api
```

## Architecture

### Project Structure
- **Api**: ASP.NET Core Web API, configuration, and middleware
- **Application**: CQRS patterns with MediatR, interfaces, and commands/queries
- **Domain**: Core domain models and entities
- **Infrastructure**: Service implementations and data access

### Key Components

#### Document Processing
- PDF text extraction with page-level chunking
- Configurable text chunk size and overlap
- Parallel processing of document chunks
- Automatic embedding generation for chunks and full documents

#### Embedding Service
- Ollama API integration with retry mechanism
- Validation of embedding dimensions (768)
- Error handling and logging
- Caching at both chunk and document level

#### Storage System
- File-based document repository with JSON serialization
- In-memory caching with configurable duration
- Thread-safe operations for concurrent access
- Optional read-only mode for production deployments

## API Endpoints

### Document Processing
```http
POST /api/Documents/process
Content-Type: multipart/form-data

file: [PDF File]
```
Processes a PDF document, generates embeddings, and stores the results.

### Semantic Search
```http
GET /api/Documents/search?query={searchText}
```
Performs semantic search across processed documents.

### Health Check
```http
GET /health
```
Returns the health status of the application and Ollama service.

## Configuration Options

### Storage Settings
- `CachePath`: Directory for embedding cache storage
- `DataPath`: Directory for processed document storage
- `ReadOnlyMode`: Prevents document modifications in production

### Ollama Settings
- `OllamaEndpoint`: Ollama API endpoint URL
- `ModelName`: Model for embedding generation
- `MaxTextLength`: Maximum text length for embedding
- `MinTextLength`: Minimum text length for embedding
- `MaxRetries`: Number of retry attempts
- `RetryDelayMs`: Delay between retries
- `ChunkSize`: Text chunk size for processing
- `ChunkOverlap`: Overlap between chunks
- `SimilarityThreshold`: Minimum similarity score
- `MaxResults`: Maximum search results

## Performance Tuning

### Caching Strategy
- In-memory cache with 24-hour duration
- File-based persistent cache with JSON storage
- Automatic cache invalidation and cleanup

### Concurrency Control
- Parallel document processing (max 3 concurrent operations)
- Thread-safe repository operations
- Connection pooling for HTTP clients

## Monitoring

### Health Checks
- Application status verification
- Ollama service connectivity check
- Embedding generation validation
- Storage system accessibility

## Production Deployment

1. Enable read-only mode in production:
```json
{
    "Storage": {
        "ReadOnlyMode": true
    }
}
```

2. Configure appropriate security headers and CORS policies
3. Set up monitoring and alerting
4. Configure OpenTelemetry collectors
5. Implement appropriate backup strategies

## Error Handling

- Automatic retry for transient failures
- Circuit breaker for external service protection
- Detailed error logging with context
- Graceful degradation strategies

## Contributing

1. Fork the repository
2. Create a feature branch
3. Implement changes with tests
4. Submit a pull request

## License

MIT License

Copyright (c) 2025 DocuRAG

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.

## Acknowledgments

- Ollama team for the embedding model
- Contributors and maintainers 
