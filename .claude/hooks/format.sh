#!/usr/bin/env bash
# PostToolUse hook: formats the C# file an edit just touched, so layout never
# reaches a diff (guidelines/agent.md §2 in core-standards-docs).
input="$(cat)"
file="$(printf '%s' "$input" | sed -n 's/.*"file_path"[[:space:]]*:[[:space:]]*"\([^"]*\)".*/\1/p' | head -n 1)"
file="${file//\\//}"
case "$file" in
  *.cs) [ -f "$file" ] && dotnet format whitespace --folder --include "$file" >/dev/null 2>&1 ;;
esac
exit 0
