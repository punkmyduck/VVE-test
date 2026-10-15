using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace VVE.Tests;

public class TaskApiTests
{
    [Fact]
    public async Task UnavailableRabbitMq_TaskIsCompleted_AndEventIsLogged()
    {
        await using var factory = new TaskApiFactory(useUnavailableRabbitMq: true);
        using var client = CreateClient(factory);
        var created = await client.PostAsJsonAsync("/tasks", new { Title = "Задача без брокера" });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var id = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var response = await client.PutAsync($"/tasks/{id}/complete", null);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var item = Assert.Single((await GetTasks(client)).EnumerateArray());
        Assert.True(item.GetProperty("isCompleted").GetBoolean());
        Assert.NotEqual(JsonValueKind.Null, item.GetProperty("completedAt").ValueKind);
        await factory.DisposeAsync();
        Assert.Contains(factory.PublisherLogger.Messages, message =>
            message.Contains(id.ToString()) && message.Contains("task.completed") && message.Contains("completedAt"));
    }

    [Fact]
    public async Task Create_Complete_Delete_Task()
    {
        await using var factory = new TaskApiFactory();
        using var client = CreateClient(factory);

        var created = await client.PostAsJsonAsync("/tasks", new { Title = "Купить молоко" });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var id = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        Assert.NotEqual(Guid.Empty, id);

        var items = await GetTasks(client);
        var item = Assert.Single(items.EnumerateArray());
        Assert.Equal(id, item.GetProperty("id").GetGuid());
        Assert.Equal("Medium", item.GetProperty("priority").GetString());
        Assert.False(item.GetProperty("isCompleted").GetBoolean());
        Assert.Equal(JsonValueKind.Null, item.GetProperty("completedAt").ValueKind);
        var createdAt = item.GetProperty("createdAt").GetDateTimeOffset();

        var completed = await client.PutAsync($"/tasks/{id}/complete", null);
        Assert.Equal(HttpStatusCode.NoContent, completed.StatusCode);
        item = Assert.Single((await GetTasks(client)).EnumerateArray());
        Assert.True(item.GetProperty("isCompleted").GetBoolean());
        var completedAt = item.GetProperty("completedAt").GetDateTimeOffset();
        Assert.True(completedAt >= createdAt);

        var repeated = await client.PutAsync($"/tasks/{id}/complete", null);
        Assert.Equal(HttpStatusCode.Conflict, repeated.StatusCode);
        item = Assert.Single((await GetTasks(client)).EnumerateArray());
        Assert.Equal(completedAt, item.GetProperty("completedAt").GetDateTimeOffset());

        var message = Assert.Single(factory.Publisher.Messages);
        Assert.Equal(id, message.TaskId);
        Assert.Equal("Купить молоко", message.Title);
        Assert.Equal(completedAt, message.CompletedAt);
        Assert.Equal(VVE.Domain.Priority.Medium, message.Priority);

        var deleted = await client.DeleteAsync($"/tasks/{id}");
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.Empty((await GetTasks(client)).EnumerateArray());
        Assert.Equal(HttpStatusCode.NotFound, (await client.DeleteAsync($"/tasks/{id}")).StatusCode);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" \t\n")]
    public async Task InvalidTitle_Returns400_WithoutCreatingTask(string? title)
    {
        await using var factory = new TaskApiFactory();
        using var client = CreateClient(factory);

        var response = await client.PostAsJsonAsync("/tasks", new { Title = title });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty((await GetTasks(client)).EnumerateArray());
    }

    [Theory]
    [InlineData(200, HttpStatusCode.Created)]
    [InlineData(201, HttpStatusCode.BadRequest)]
    public async Task TitleLength_IsValidated(int length, HttpStatusCode expected)
    {
        await using var factory = new TaskApiFactory();
        using var client = CreateClient(factory);

        var response = await client.PostAsJsonAsync("/tasks", new { Title = new string('a', length) });

        Assert.Equal(expected, response.StatusCode);
    }

    [Theory]
    [InlineData("{\"title\":\"Задача\",\"priority\":999}")]
    [InlineData("{\"title\":\"Задача\",\"priority\":\"Unknown\"}")]
    [InlineData("{\"title\":")]
    [InlineData("null")]
    public async Task InvalidBody_Returns400(string json)
    {
        await using var factory = new TaskApiFactory();
        using var client = CreateClient(factory);
        using var body = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

        var response = await client.PostAsync("/tasks", body);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty((await GetTasks(client)).EnumerateArray());
    }

    [Theory]
    [InlineData("not-a-guid")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    public async Task InvalidId_Returns400(string id)
    {
        await using var factory = new TaskApiFactory();
        using var client = CreateClient(factory);

        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsync($"/tasks/{id}/complete", null)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.DeleteAsync($"/tasks/{id}")).StatusCode);
    }

    [Fact]
    public async Task MissingTask_Returns404()
    {
        await using var factory = new TaskApiFactory();
        using var client = CreateClient(factory);
        var id = Guid.NewGuid();

        Assert.Equal(HttpStatusCode.NotFound, (await client.PutAsync($"/tasks/{id}/complete", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.DeleteAsync($"/tasks/{id}")).StatusCode);
    }

    private static HttpClient CreateClient(TaskApiFactory factory)
    {
        return factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });
    }

    private static async Task<JsonElement> GetTasks(HttpClient client)
    {
        var response = await client.GetAsync("/tasks");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }
}
