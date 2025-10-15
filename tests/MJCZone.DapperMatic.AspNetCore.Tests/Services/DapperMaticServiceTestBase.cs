using Microsoft.AspNetCore.Mvc.Testing;
using MJCZone.DapperMatic.AspNetCore.Services;
using MJCZone.DapperMatic.AspNetCore.Tests.Factories;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace MJCZone.DapperMatic.AspNetCore.Tests.Services;

public class DapperMaticServiceTestBase : IClassFixture<TestcontainersAssemblyFixture>
{
    private readonly TestcontainersAssemblyFixture _fixture;
    protected ITestOutputHelper Log { get; }

    public DapperMaticServiceTestBase(
        TestcontainersAssemblyFixture fixture,
        ITestOutputHelper outputHelper
    )
    {
        _fixture = fixture;
        Log = outputHelper;

        Log.WriteLine($"Running tests against: {this.GetType().FullName}");
    }

    protected WebApplicationFactory<Program> GetDefaultWebApplicationFactory()
    {
        return new WafWithInMemoryDatasourceRepository(_fixture.GetTestDatasources());
    }

    protected IDapperMaticService GetDapperMaticService(WebApplicationFactory<Program> factory)
    {
        return factory.Services.GetRequiredService<IDapperMaticService>();
    }
}
