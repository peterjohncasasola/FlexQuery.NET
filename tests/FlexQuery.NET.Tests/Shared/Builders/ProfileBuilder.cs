namespace FlexQuery.NET.Tests.Shared.Builders;

public class ProfileBuilder
{
    private static int _nextId = 1;
    private int _id;
    private string? _bio;
    private string? _preferredLanguage;
    private int _loyaltyPoints;

    public ProfileBuilder WithId(int id) { _id = id; return this; }
    public ProfileBuilder WithBio(string? bio) { _bio = bio; return this; }
    public ProfileBuilder WithPreferredLanguage(string? preferredLanguage) { _preferredLanguage = preferredLanguage; return this; }
    public ProfileBuilder WithLoyaltyPoints(int loyaltyPoints) { _loyaltyPoints = loyaltyPoints; return this; }

    public Profile Build()
    {
        return new Profile
        {
            Id = _id == 0 ? _nextId++ : _id,
            Bio = _bio,
            PreferredLanguage = _preferredLanguage,
            LoyaltyPoints = _loyaltyPoints
        };
    }
}
