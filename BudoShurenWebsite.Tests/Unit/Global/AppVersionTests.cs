using System.Diagnostics;
using BudoShurenWebsite.Global;

namespace BudoShurenWebsite.Tests.Unit.Global;

[Trait("Category", "Unit")]
public class AppVersionTests
{
    [Fact]
    public void Text_ist_die_FileVersion_der_Website()
    {
        var dateiVersion = FileVersionInfo.GetVersionInfo(typeof(AppVersion).Assembly.Location).FileVersion;

        AppVersion.Text.ShouldBe(dateiVersion);
        AppVersion.Text.ShouldMatch(@"^\d+\.\d+\.\d+\.\d+$");
    }
}
