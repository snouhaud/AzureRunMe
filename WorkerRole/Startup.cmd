REM I'm running at startup with elevated privileges
if exist started.txt goto skip
REM Run this section only once
:skip
time /t > started.txt

