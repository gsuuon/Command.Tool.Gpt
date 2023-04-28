# gpt

🤖 gpt in ur terminal


## Install
```
dotnet pack
dotnet tool install --global --add-source ./nupkg Gsuuon.Tool.Gpt
```

## Usage

Takes prompt from stdin and appends the first argument if it's there. Make sure `OPENAI_API_KEY` is in your environment.

```
echo 'Why did the chicken cross the road?' | gpt
```
```
git diff --staged | gpt 'Create a simple commit message'
```
