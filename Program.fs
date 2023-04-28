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
    | Reason of string
    | Partial of string
    | Error

let parse =
    function
    | "" | null -> Empty
    | x when x.Trim() = "" -> Empty
    | evtText ->
        match Regex.Match(evtText, "data: (.+)") with
        | m when m.Success ->
            let data = m.Groups.[1].Value

            try
                let choice = (JsonDocument.Parse data).RootElement.GetProperty("choices").[0]

                match choice.GetProperty("delta").TryGetProperty("content") with
                | true, content ->
                    Partial <| content.GetString()
                | _ ->
                    match choice.GetProperty("finish_reason").GetString() with
                    | "" | null -> Empty
                    | reason -> Reason reason

            with
            | :? JsonException ->
                match m.Value with
                | "[DONE]" -> Done
                | x -> failwithf "Unparsed response: %s" x
            | e -> reraise ()
        | _ ->
            Error

let openai (msg: string) =
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
        "-H"; $"Authorization: Bearer {OPENAI_API_KEY}"
        "--data-binary"; body
    ]

    let req = proc("curl", args)

    { req with
        stdout = req <!> Stdout |> transform (fun write line ->
            match parse line with
            | Partial x ->
                write x
            | Done ->
                write "\n" // make sure we have at least one newline
            | Reason "limit" ->
                slogn [
                    fg Color.Red
                    bg Color.White
                ] "[Token limit]"
            | Error ->
               slogn [fg Color.White; bg Color.Red] line
            | Reason _ | Empty -> ()
            )
    }

let show (x: Proc) =
    x <!> Stdout |> consume log
    x.proc.WaitForExit()

    if x.proc.ExitCode <> 0 then
        x <!> Stderr |> consume (slogn [fg Color.Red])

let prompt = 
    let stdin = host.stdin |> readNow
    let arg = Environment.GetCommandLineArgs() |> Array.tryItem 1

    match stdin, arg with
    | stdin, Some arg -> stdin + "\n" + arg
    | stdin, _ -> stdin

prompt |> openai |> show
