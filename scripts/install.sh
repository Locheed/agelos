#!/bin/bash

set -e

echo "Installing Agelos..."

OS=$(uname -s | tr '[:upper:]' '[:lower:]')
ARCH=$(uname -m)

case "$ARCH" in
    x86_64)        ARCH="x64" ;;
    aarch64|arm64) ARCH="arm64" ;;
    *)
        echo "Unsupported architecture: $ARCH"
        exit 1
        ;;
esac

case "$OS" in
    linux)  OS="linux" ;;
    darwin) OS="darwin" ;;
    *)
        echo "Unsupported OS: $OS"
        exit 1
        ;;
esac

BINARY="agelos-${OS}-${ARCH}"
URL="https://github.com/yourname/agelos/releases/latest/download/${BINARY}"

echo "Downloading agelos for ${OS}-${ARCH}..."
curl -L "$URL" -o /tmp/agelos

echo "Installing to /usr/local/bin/agelos..."
sudo mv /tmp/agelos /usr/local/bin/agelos
sudo chmod +x /usr/local/bin/agelos

echo ""
echo "Agelos installed successfully!"
echo ""
agelos --version
echo ""
echo "Get started:"
echo "  mkdir my-project && cd my-project"
echo "  agelos run opencode"
echo ""
