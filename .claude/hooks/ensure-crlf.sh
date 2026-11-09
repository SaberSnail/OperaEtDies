#!/usr/bin/env bash
# PostToolUse hook: convert files written by Claude's Write/Edit tools to CRLF line endings.
# Reads the hook JSON from stdin and pulls out tool_input.file_path.
file=$(perl -MJSON::PP -0777 -ne 'my $d = eval { decode_json($_) } or exit; print $d->{tool_input}{file_path} // ""')
[ -n "$file" ] || exit 0

file=$(cygpath -u "$file")
project=$(cygpath -u "${CLAUDE_PROJECT_DIR:-$PWD}")

# Only touch existing files inside the project (skips scratchpad, memory, etc.).
case "$file" in
	"$project"/*) ;;
	*) exit 0 ;;
esac
[ -f "$file" ] || exit 0

# Shell scripts must keep LF endings or bash cannot run them.
case "$file" in *.sh) exit 0 ;; esac

# unix2dos skips binary files on its own.
unix2dos -q "$file" 2>/dev/null
exit 0
