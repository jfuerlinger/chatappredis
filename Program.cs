﻿using Azure.AI.OpenAI;
using Azure.Core;
using Json.Schema.Generation.Intents;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.SemanticKernel.Memory;
using Microsoft.SemanticKernel.Connectors.Redis;
using Microsoft.SemanticKernel.Text;
using System;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using StackExchange.Redis;

string aoaiEndpoint = "https://<your_Azure_openai_name>.openai.azure.com/";
string aoaiApiKey = "Azure+openai_access_key";
string redisConnection = "Connection_String_for_Redis_Cache_With_RediSearch_Module";
string aoaiModel = "gpt_model_name";
string aoaiEmbeddingModel = "ada_model_name";

//Kernel kernel = 
//    new IKernelBuilder().WithAzureOpenAIChatCompletionService(aoaiModel, aoaiEndpoint, aoaiApiKey)
//    .Build();

var builder = Kernel.CreateBuilder();
builder.AddAzureOpenAIChatCompletion(aoaiModel, aoaiEndpoint, aoaiApiKey);
#pragma warning disable SKEXP0011 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
builder.AddAzureOpenAITextEmbeddingGeneration("TextEmbeddingAda002_1", aoaiEndpoint, aoaiApiKey);
#pragma warning restore SKEXP0011 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.

Kernel kernel = builder.Build();

// See https://stackexchange.github.io/StackExchange.Redis/Basics#basic-usage
ConnectionMultiplexer connectionMultiplexer = await ConnectionMultiplexer.ConnectAsync(redisConnection);
IDatabase database = connectionMultiplexer.GetDatabase();
#pragma warning disable SKEXP0027 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
RedisMemoryStore memoryStore = new RedisMemoryStore(database, vectorSize: 1536);
#pragma warning restore SKEXP0027 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.

#pragma warning restore SKEXP0052 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
#pragma warning restore SKEXP0003 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
string collectionName = "Fsharpupdate";
#pragma warning disable SKEXP0003 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
#pragma warning disable SKEXP0052 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
#pragma warning disable SKEXP0011 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
ISemanticTextMemory memory = new MemoryBuilder()
        .WithLoggerFactory(kernel.LoggerFactory)
        .WithMemoryStore(memoryStore)
        .WithAzureOpenAITextEmbeddingGeneration(aoaiEmbeddingModel, aoaiEndpoint, aoaiApiKey)
        .Build();
#pragma warning restore SKEXP0011 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
#pragma warning restore SKEXP0052 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
#pragma warning restore SKEXP0003 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
using (HttpClient client = new())
{
    string s = await client.GetStringAsync("https://devblogs.microsoft.com/dotnet/overhauled-fsharp-code-fixes-in-visual-studio/");
#pragma warning disable SKEXP0055 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
    List<string> paragraphs =
        TextChunker.SplitPlainTextParagraphs(
            TextChunker.SplitPlainTextLines(
                WebUtility.HtmlDecode(Regex.Replace(s, @"<[^>]+>|&nbsp;", "")),
                128),
            1024);
#pragma warning restore SKEXP0055 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.

    for (int i = 0; i < paragraphs.Count; i++)
        await memory.SaveInformationAsync(collectionName, paragraphs[i], $"paragraph{i}");
}



string TimePrompt = @$"
Today is: {DateTime.UtcNow:r}
Current time is: {DateTime.UtcNow:r}

Answer to the following questions using JSON syntax, including the data used.
Is it morning, afternoon, evening, or night (morning/afternoon/evening/night)?
Is it weekend time (weekend/not weekend)?
";

// Create a Semantic Kernel template for chat
var promptFunction = kernel.CreateFunctionFromPrompt(
    TimePrompt+ @"    
    {{$history}}
    User: {{$request}}
    Assistant:
    "
);

// Create a new chat
ChatHistory chat = [new ChatMessageContent(AuthorRole.System, "You are an AI assistant that helps people find information.")];
StringBuilder stbuilder = new();

// Start the chat loop
while (true)
{
    Console.Write("Question: ");
    string question = Console.ReadLine()!;

    stbuilder.Clear();
    await foreach (var result in memory.SearchAsync(collectionName, question, limit: 3))
        stbuilder.AppendLine(result.Metadata.Text);
    int contextToRemove = -1;
    if (stbuilder.Length != 0)
    {
        stbuilder.Insert(0, "Here's some additional information: ");
        contextToRemove = chat.Count;
        chat.AddUserMessage(stbuilder.ToString());
    }

    chat.AddUserMessage(question);

    stbuilder.Clear();
    await foreach (StreamingChatMessageContent message in kernel.InvokeStreamingAsync<StreamingChatMessageContent>(
        promptFunction,
        new() {
            { "request", question },
            { "history", string.Join("\n", chat.Select(x => x.Role + ": " + x.Content)) }

        }))
    {
        Console.Write(message);
        stbuilder.Append(message);
    }
    Console.WriteLine();
    chat.AddAssistantMessage(stbuilder.ToString());

    if (contextToRemove >= 0) chat.RemoveAt(contextToRemove);
    Console.WriteLine();
}
