#!/bin/bash
# BitgetLab Build Script
# This script builds all .NET projects in the solution
#
# Usage:
#   ./build.sh          # Build in Release mode (default)
#   ./build.sh Debug    # Build in Debug mode
#
# Note: Ensure this script has executable permissions:
#   chmod +x build.sh

set -e  # Exit on error

echo "======================================"
echo "BitgetLab Build Script"
echo "======================================"
echo ""

# Colors for output
GREEN='\033[0;32m'
RED='\033[0;31m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# Configuration
CONFIGURATION="${1:-Release}"
SOLUTION_FILE="BitgetLab.sln"

echo -e "${YELLOW}Configuration: ${CONFIGURATION}${NC}"
echo ""

# Check if .NET SDK is installed
if ! command -v dotnet &> /dev/null; then
    echo -e "${RED}Error: .NET SDK is not installed${NC}"
    echo "Please install .NET 8 SDK: https://dotnet.microsoft.com/download"
    exit 1
fi

# Display .NET version
echo "Using .NET SDK:"
dotnet --version
echo ""

# Clean previous builds
echo -e "${YELLOW}Cleaning previous builds...${NC}"
dotnet clean "$SOLUTION_FILE" -c "$CONFIGURATION" --nologo -v q || true
echo ""

# Restore dependencies
echo -e "${YELLOW}Restoring dependencies...${NC}"
dotnet restore "$SOLUTION_FILE" --nologo
echo ""

# Build solution
echo -e "${YELLOW}Building solution...${NC}"
dotnet build "$SOLUTION_FILE" -c "$CONFIGURATION" --no-restore --nologo

# Check build status
if [ $? -eq 0 ]; then
    echo ""
    echo -e "${GREEN}======================================"
    echo -e "Build succeeded!"
    echo -e "======================================${NC}"
    echo ""
    echo "Build outputs:"
    echo "  - BitgetLab.Api:       src/BitgetLab.Api/bin/${CONFIGURATION}/net8.0/"
    echo "  - BitgetLab.Core:      src/BitgetLab.Core/bin/${CONFIGURATION}/net8.0/"
    echo "  - BitgetLab.Worker:    src/BitgetLab.Worker/bin/${CONFIGURATION}/net8.0/"
    echo "  - BitgetLab.Simulator: src/BitgetLab.Simulator/bin/${CONFIGURATION}/net8.0/"
    echo ""
    echo "To run the API:"
    echo "  cd src/BitgetLab.Api && dotnet run"
    echo ""
    echo "To publish for deployment:"
    echo "  dotnet publish src/BitgetLab.Api -c Release -o publish/api"
    echo "  dotnet publish src/BitgetLab.Worker -c Release -o publish/worker"
else
    echo ""
    echo -e "${RED}======================================"
    echo -e "Build failed!"
    echo -e "======================================${NC}"
    exit 1
fi
