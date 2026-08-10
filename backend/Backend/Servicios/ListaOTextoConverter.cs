using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Backend.Servicios;

public class ListaOTextoConverter : JsonConverter<List<string>>
{
    public override List<string> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            var texto = reader.GetString() ?? string.Empty;
            return new List<string> { texto };
        }

        if (reader.TokenType == JsonTokenType.StartArray)
        {
            var lista = new List<string>();
            while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
            {
                if (reader.TokenType == JsonTokenType.String)
                {
                    lista.Add(reader.GetString() ?? string.Empty);
                }
            }
            return lista;
        }

        throw new JsonException("El campo debe ser un texto o un arreglo de textos.");
    }

    public override void Write(Utf8JsonWriter writer, List<string> value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();
        foreach (var item in value)
        {
            writer.WriteStringValue(item);
        }
        writer.WriteEndArray();
    }
}