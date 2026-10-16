#!/bin/sh
# Сеанс xrdp для Аверса: вместо рабочего стола Linux сразу открывается программа.
# Сотрудник закрыл Аверс — сеанс завершается.
export LANG=ru_RU.UTF-8

# Раскладки us,ru с переключением Alt+Shift, как в Windows. xrdp сбрасывает раскладку
# при каждом подключении, поэтому возвращаем свою, пока жив сеанс.
(
  while sleep 3; do
    query=$(setxkbmap -query 2>/dev/null) || exit 0
    echo "$query" | grep -q alt_shift_toggle || setxkbmap -layout us,ru -option grp:alt_shift_toggle
  done
) &

openbox &
exec /opt/avers/Avers
