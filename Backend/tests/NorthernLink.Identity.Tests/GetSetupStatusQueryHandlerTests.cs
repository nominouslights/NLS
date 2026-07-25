using NorthernLink.Identity.Application.Auth.Setup;
using Xunit;

namespace NorthernLink.Identity.Tests;

public class GetSetupStatusQueryHandlerTests
{
    [Fact]
    public async Task Empty_users_table_reports_setup_required()
    {
        var handler = new GetSetupStatusQueryHandler(new InMemoryUserRepository());

        var result = await handler.Handle(new GetSetupStatusQuery(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.SetupRequired);
    }

    [Fact]
    public async Task Any_existing_user_reports_setup_closed()
    {
        var userRepository = new InMemoryUserRepository();
        userRepository.Add(TestUsers.Create());
        var handler = new GetSetupStatusQueryHandler(userRepository);

        var result = await handler.Handle(new GetSetupStatusQuery(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value.SetupRequired);
    }
}
