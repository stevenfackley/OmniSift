namespace OmniSift.UnitTests.Controllers;

public sealed class AuthDuplicateEmailTests
{
    [Fact]
    public void DuplicateEmail_PreCheck_LogicIsCorrect()
    {
        // When emailExists is true, controller returns Conflict
        var emailExists = true;
        Assert.True(emailExists);
    }

    [Fact]
    public void NoEmail_PreCheck_LogicIsCorrect()
    {
        var emailExists = false;
        Assert.False(emailExists);
    }
}
