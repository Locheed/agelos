#!/bin/bash

REQUEST_DIR="/workspace/.agelos"
REQUEST_FILE="$REQUEST_DIR/runtime-request"

mkdir -p "$REQUEST_DIR"

request_runtime() {
  local runtime="$1"
  echo "RUNTIME_REQUEST:$runtime" > "$REQUEST_FILE"
  echo "Waiting for runtime $runtime to be installed..."

  while [ -f "$REQUEST_FILE" ]; do
    sleep 1
  done

  echo "Runtime $runtime is now available"
}

safe_exec() {
  local cmd="$1"
  shift

  if ! command -v "$cmd" &> /dev/null; then
    case "$cmd" in
      kotlinc)
        request_runtime "kotlin:1.9"
        ;;
      dotnet)
        if ls *.csproj &> /dev/null; then
          version=$(grep -oP 'net\K\d+' *.csproj | head -1)
          request_runtime "dotnet:$version"
        else
          request_runtime "dotnet:10"
        fi
        ;;
      node|npm)
        request_runtime "node:20"
        ;;
      python3|python)
        request_runtime "python:3.12"
        ;;
      go)
        request_runtime "go:1.22"
        ;;
      *)
        echo "Command not found: $cmd"
        echo "You may need to install it manually or add it to .agelos.yml"
        return 1
        ;;
    esac
  fi

  "$cmd" "$@"
}

export -f safe_exec
export -f request_runtime

exec opencode "$@"

