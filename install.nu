dotnet pack

try {
  dotnet tool update --global --add-source ./nupkg Gsuuon.Tool.Gpt
} catch {
  dotnet tool install --global --add-source ./nupkg Gsuuon.Tool.Gpt
}
