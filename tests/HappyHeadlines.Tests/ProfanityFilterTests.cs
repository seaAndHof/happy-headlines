using HappyHeadlines.Shared;

namespace HappyHeadlines.Tests;

public class ProfanityFilterTests
{
    [Fact]
    public void Masks_whole_words_regardless_of_case()
    {
        Assert.Equal("****, what a ****** idea", ProfanityFilter.Mask("Damn, what a stupid idea", ["damn", "stupid"]));
    }

    [Fact]
    public void Leaves_words_that_only_contain_a_profanity()
    {
        Assert.Equal("Dumbledore approves", ProfanityFilter.Mask("Dumbledore approves", ["dumb"]));
    }
}
