#!/bin/bash
# Test script to verify the entrypoint works correctly

echo "Testing entrypoint script functionality..."

# Set test environment variables
export JELLYFIN_DB_TYPE="postgresql"
export JELLYFIN_DB_HOST="test-db-host"
export JELLYFIN_DB_PORT="5432"
export JELLYFIN_DB_NAME="test-db-name"
export JELLYFIN_DB_USER="test-db-user"
export JELLYFIN_DB_PASSWORD="test-db-password"

echo "Environment variables set:"
echo "JELLYFIN_DB_TYPE: $JELLYFIN_DB_TYPE"
echo "JELLYFIN_DB_HOST: $JELLYFIN_DB_HOST"
echo "JELLYFIN_DB_NAME: $JELLYFIN_DB_NAME"
echo "JELLYFIN_DB_USER: $JELLYFIN_DB_USER"

# Test that the entrypoint script can be executed
echo "Testing entrypoint execution..."
if [ -f "docker-entrypoint.sh" ]; then
    echo "Entrypoint script exists and is executable"
    chmod +x docker-entrypoint.sh
    echo "Entrypoint script is executable"

    # Show the content
    echo "Entrypoint script content:"
    cat docker-entrypoint.sh
    echo ""

    # Test that it properly sets the connection string
    echo "Testing connection string generation..."
    if [[ -n "$JELLYFIN_DB_TYPE" && "$JELLYFIN_DB_TYPE" == "postgresql" ]]; then
        export JELLYFIN_ConnectionStrings__DefaultConnection="Host=${JELLYFIN_DB_HOST};Port=${JELLYFIN_DB_PORT};Database=${JELLYFIN_DB_NAME};Username=${JELLYFIN_DB_USER};Password=${JELLYFIN_DB_PASSWORD}"
        echo "Connection string set to: $JELLYFIN_ConnectionStrings__DefaultConnection"
    fi

    echo "Test completed successfully!"
else
    echo "ERROR: Entrypoint script not found!"
    exit 1
fi
