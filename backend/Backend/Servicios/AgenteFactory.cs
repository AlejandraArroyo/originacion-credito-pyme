using System;
using System.ClientModel;
using System.Collections.Generic;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OpenAI;

namespace Backend.Servicios;

public class AgenteFactory
{
    private readonly string _apiKey;
    private readonly string _modelo;

    public AgenteFactory(IConfiguration config)
    {
        _apiKey = config["OpenRouter:ApiKey"]
            ?? throw new InvalidOperationException("Falta OpenRouter:ApiKey en appsettings");
        _modelo = config["OpenRouter:Modelo"]
            ?? throw new InvalidOperationException("Falta OpenRouter:Modelo en appsettings");
    }

    public AIAgent CrearAgente(string instrucciones, IEnumerable<AITool>? tools = null)
    {
        var clientOptions = new OpenAIClientOptions
        {
            Endpoint = new Uri("https://openrouter.ai/api/v1")
        };

        var client = new OpenAIClient(new ApiKeyCredential(_apiKey), clientOptions);
        var chatClient = client.GetChatClient(_modelo).AsIChatClient();
        return chatClient.AsAIAgent(
            instructions: instrucciones,
            name: "AsistenteOriginacion",
            tools: tools is null ? null : new List<AITool>(tools).ToArray()
        );
    }
}