open Gsuuon.Command

open Gsuuon.Console.Log
open Gsuuon.Console.Style

open System
open System.Drawing
open System.Text.Json
open System.Text.RegularExpressions

let OPENAI_API_KEY = Environment.GetEnvironmentVariable "OPENAI_API_KEY"

type Item =
    | Empty
    | Done
    | StopReason of string
    | Partial of string
    | Error

let (|RegexMatches|_|) reg x =
    match Regex.Match(x, reg) with
    | m when m.Success -> Some m.Groups
    | _ -> None
    
let parse line =
    match line with
    | "" | null | _ when line.Trim() = "" -> Empty
    | RegexMatches "data: (.+)" m ->
        let data = m.[1].Value

        try
            let choice =
                (JsonDocument.Parse data)
                    .RootElement.GetProperty("choices").[0]

            match choice.GetProperty("delta").TryGetProperty("content") with
            | true, content -> content.GetString() |> Partial
            | _ ->
                match choice.GetProperty("finish_reason").GetString() with
                | "" | null -> Empty
                | reason -> StopReason reason

        with
        | :? JsonException ->
            match data with
            | "[DONE]" -> Done
            | x -> failwithf "Unparsed response: %s" x
        | e -> reraise ()
    | _ ->
        Error

let openai key (msg: string) =
    let body = JsonSerializer.Serialize {|
        model = "gpt-3.5-turbo"
        messages = [|
            {|
              role = "user"
              content = msg
            |}
        |]
        stream = true
    |}

    let args = [
        "https://api.openai.com/v1/chat/completions"
        "-N"
        "-H"; "Content-Type: application/json"
        "-H"; $"Authorization: Bearer {key}"
        "--data-binary"; body
    ]

    proc("curl", args)

match OPENAI_API_KEY with
| "" | null -> eprintfn "Missing OPENAI_API_KEY"
| key ->
    let prompt = 
        let stdin = host.stdin |> readNow
        let arg = Environment.GetCommandLineArgs() |> Array.tryItem 1

        match stdin, arg with
        | stdin, Some arg -> stdin + "\n" + arg
        | stdin, _ -> stdin

    prompt
    |> openai key <!> Stdout
    |> consume (fun line ->
        match parse line with
        | Partial x ->
            log x
        | StopReason "limit" ->
            slogn [
                fg Color.Red
                bg Color.White
            ] "[token limit]"
        | Error ->
            eprintf "%s" line
        | _ -> ()
    )
