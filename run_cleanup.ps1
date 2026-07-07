$npgsql = (Get-ChildItem -Path .\packages -Filter Npgsql.dll -Recurse | Select-Object -First 1).FullName
& 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe' /r:$npgsql /out:cleanup_db.exe cleanup_db.cs
.\cleanup_db.exe
