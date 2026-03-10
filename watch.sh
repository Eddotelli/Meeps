#!/bin/bash
echo "========================================"
echo "  Meeps - BFF Pattern (Backend for Frontend)"
echo "========================================"
echo ""
echo "Starting API with embedded Blazor Client..."
echo "Client will be served from: https://localhost:7000"
echo "Swagger UI available at: https://localhost:7000/swagger"
echo ""

# Get the directory where the script is located
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

cd "$SCRIPT_DIR/API"
dotnet watch --launch-profile https

cd "$SCRIPT_DIR"
