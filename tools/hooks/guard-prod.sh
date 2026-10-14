#!/usr/bin/env bash
# PreToolUse-хук Claude Code: не даёт выполнить команду, которая пишет в боевые базы Аверса.
# Боевые: Oracle servora/avers102, PostgreSQL в сети 192.168.37.x.
input="$(cat)"
if printf '%s' "$input" | grep -Eiq '(servora|avers102|avers81|192\.168\.37\.)' &&
   printf '%s' "$input" | grep -Eiq '\b(insert|update|delete|merge|drop|alter|truncate|create|grant|revoke)\b'; then
  echo "Заблокировано: похоже на запись в боевую базу Аверса. Работайте с копией на стенде." >&2
  exit 2
fi
exit 0
