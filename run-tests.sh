#!/bin/bash

echo "================================================"
echo "  Relife TimeGuard - Building and Testing"
echo "================================================"
echo ""

echo "Step 1: Restoring packages..."
dotnet restore

echo ""
echo "Step 2: Building Relife.Core..."
dotnet build Relife.Core/Relife.Core.csproj --configuration Release

if [ $? -ne 0 ]; then
    echo "❌ Build failed!"
    exit 1
fi

echo ""
echo "Step 3: Building Relife.Core.Tests..."
dotnet build Relife.Core.Tests/Relife.Core.Tests.csproj --configuration Release

if [ $? -ne 0 ]; then
    echo "❌ Test build failed!"
    exit 1
fi

echo ""
echo "Step 4: Running all tests..."
echo "================================================"
dotnet test Relife.Core.Tests/Relife.Core.Tests.csproj --configuration Release --verbosity normal

echo ""
echo "================================================"
echo "  Test run completed!"
echo "================================================"
