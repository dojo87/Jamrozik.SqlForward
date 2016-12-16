REM Build Solution
SET CONFIGURATION=%1
set PATH_SOURCE_SLN="%cd%\Jamrozik.SqlForward.sln"
if [%1]==[] (
  SET CONFIGURATION=NET462
)
MSBuild %PATH_SOURCE_SLN% /p:Configuration=%CONFIGURATION%