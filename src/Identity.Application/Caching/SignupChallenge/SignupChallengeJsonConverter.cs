using System.Text.Json;
using System.Text.Json.Serialization;

namespace Identity.Application.Caching.SignupChallenge;

public class SignupChallengeJsonConverter : JsonConverter<SignupChallenge>
{
    public override SignupChallenge Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var jsonObject = JsonDocument.ParseValue(ref reader).RootElement;
        var id = jsonObject.GetProperty("Id").GetGuid();
        var challengeValue = jsonObject.GetProperty("ChallengeValue").GetString();
        var difficulty = jsonObject.GetProperty("Difficulty").GetInt32();
        var createdAt = jsonObject.GetProperty("CreatedAt").GetDateTime();
        var expiresAt = jsonObject.GetProperty("ExpiresAt").GetDateTime();

        return SignupChallenge.FromCache(id, challengeValue!, difficulty, createdAt, expiresAt);
    }

    public override void Write(Utf8JsonWriter writer, SignupChallenge value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteString("Id", value.Id);
        writer.WriteString("ChallengeValue", value.ChallengeValue);
        writer.WriteNumber("Difficulty", value.Difficulty);
        writer.WriteString("CreatedAt", value.CreatedAt);
        writer.WriteString("ExpiresAt", value.ExpiresAt);
        writer.WriteEndObject();
    }
}
