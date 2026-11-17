$connString = "Server=DESKTOP-OVKNQTJ\SQLEXPRESS;Database=FaceStone;Trusted_Connection=True;TrustServerCertificate=True;"
$conn = New-Object System.Data.SqlClient.SqlConnection($connString)
$conn.Open()
Write-Host "Database Connected Successfully to: $($conn.Database)"

$cmd = $conn.CreateCommand()
$cmd.CommandText = "SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE = 'BASE TABLE' ORDER BY TABLE_NAME"
$reader = $cmd.ExecuteReader()
Write-Host "`n=== TABLES ==="
while ($reader.Read()) {
    Write-Host $reader[0]
}
$reader.Close()

$cmd.CommandText = "SELECT COLUMN_NAME, DATA_TYPE, IS_NULLABLE FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'WarehouseBatches' ORDER BY ORDINAL_POSITION"
$reader = $cmd.ExecuteReader()
Write-Host "`n=== WarehouseBatches Columns ==="
while ($reader.Read()) {
    Write-Host "$($reader[0]) | $($reader[1]) | $($reader[2])"
}
$reader.Close()

$cmd.CommandText = "SELECT COLUMN_NAME, DATA_TYPE, IS_NULLABLE FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'ProductionFinishedProducts' ORDER BY ORDINAL_POSITION"
$reader = $cmd.ExecuteReader()
Write-Host "`n=== ProductionFinishedProducts Columns ==="
while ($reader.Read()) {
    Write-Host "$($reader[0]) | $($reader[1]) | $($reader[2])"
}
$reader.Close()

$conn.Close()
