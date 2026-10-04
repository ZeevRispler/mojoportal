using mojoPortal.Business;
using mojoPortal.Web.Services;
using Xunit;

namespace mojoPortal.Tests.Unit;

public sealed class ServiceTests
{
    [Fact]
    public void Password_hasher_roundtrips()
    {
        var h = new MojoPasswordHasher();
        var (hash, salt) = h.Hash("s3cret!");
        Assert.True(h.Verify("s3cret!", hash, salt));
        Assert.False(h.Verify("other", hash, salt));
    }

    [Fact]
    public void GetCurrentPageUrl_strips_tilde()
    {
        Assert.Equal("/home", MojoContext.GetCurrentPageUrl(new SitePage { Url = "~/home" }));
        Assert.Equal("/x", MojoContext.GetCurrentPageUrl(new SitePage { Url = "/x" }));
        Assert.Equal("/", MojoContext.GetCurrentPageUrl(null));
    }

    [Fact]
    public void ClaimsMojoUser_anonymous_when_no_principal()
    {
        var accessor = new Microsoft.AspNetCore.Http.HttpContextAccessor();
        var anon = new ClaimsMojoUser(accessor);
        Assert.False(anon.IsAuthenticated);
        Assert.Equal(-1, anon.UserId);
        Assert.Equal(Guid.Empty, anon.UserGuid);
    }
}
