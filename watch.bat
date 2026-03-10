@echo off
echo ========================================
echo   Meeps - BFF Pattern (Backend for Frontend)
echo ========================================
echo.
echo Starting API with embedded Blazor Client...
echo Client will be served from: https://localhost:7000
echo Swagger UI available at: https://localhost:7000/swagger
echo.

cd /d "%~dp0API"
dotnet watch --launch-profile https

cd /d "%~dp0"