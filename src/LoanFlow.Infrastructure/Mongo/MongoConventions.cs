using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Bson.Serialization.Serializers;

namespace LoanFlow.Infrastructure.Mongo;

/// <summary>
/// Process-wide serialization settings for the MongoDB driver. They are static
/// (the driver's BsonSerializer is global), so they're registered once.
/// </summary>
public static class MongoConventions
{
    private static int _registered;

    public static void Register()
    {
        if (Interlocked.Exchange(ref _registered, 1) == 1)
        {
            return;
        }

        ConventionRegistry.Register(
            "LoanFlow",
            new ConventionPack
            {
                // "applicantName" rather than "ApplicantName" in the documents.
                new CamelCaseElementNameConvention(),
                // "Approved" rather than 2: readable in Compass, and safe if the enum is reordered.
                new EnumRepresentationConvention(BsonType.String),
                // Old documents with a removed field still deserialize.
                new IgnoreExtraElementsConvention(true),
            },
            type => type.Namespace?.StartsWith("LoanFlow", StringComparison.Ordinal) == true);

        // Driver 3.x refuses to serialize a Guid until you pick a representation.
        // Standard = the portable UUID format (BSON binary subtype 4).
        BsonSerializer.TryRegisterSerializer(new GuidSerializer(GuidRepresentation.Standard));

        // decimal needs nothing here: driver 3.x stores it as Decimal128 by default
        // (2.x stored strings), so amounts stay exact and sortable.
    }
}
