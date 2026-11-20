using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Xunit;

namespace PetstoreTests.XUnitTests;

public class PetstoreApiTestsXUnit : IDisposable
{
    private readonly Api _api = new();

    public void Dispose() => _api.Dispose();


    [Fact]
    public async Task Pet_Create_ReturnsCreatedPet()
    {
        var pet = NewPet();
        using var res = await _api.Send(HttpMethod.Post, "/pet", pet);

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var created = await _api.Read<Pet>(res);
        Assert.NotNull(created);
        Assert.Equal(pet.Id, created!.Id);
        Assert.Equal(pet.Name, created.Name);
    }

    [Fact]
    public async Task Pet_GetById_Existing_ReturnsPet()
    {
        var pet = await CreatePetAsync();
        using var res = await _api.Send(HttpMethod.Get, $"/pet/{pet.Id}");

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var found = await _api.Read<Pet>(res);
        Assert.Equal(pet.Id, found?.Id);
    }

    [Fact]
    public async Task Pet_Update_ChangesName()
    {
        var pet = await CreatePetAsync();
        var renamed = pet with { Name = pet.Name + "-v2" };
        (await _api.Send(HttpMethod.Put, "/pet", renamed)).Dispose();

        var updated = await Api.Eventually(async () =>
        {
            using var get = await _api.Send(HttpMethod.Get, $"/pet/{pet.Id}");
            return (await _api.Read<Pet>(get))?.Name == renamed.Name;
        });

        Assert.True(updated, "Имя питомца не обновилось");
    }

    [Fact]
    public async Task Pet_Delete_ThenGetReturnsNotFound()
    {
        var pet = await CreatePetAsync();
        (await _api.Send(HttpMethod.Delete, $"/pet/{pet.Id}")).Dispose();

        var deleted = await Api.Eventually(async () =>
        {
            using var get = await _api.Send(HttpMethod.Get, $"/pet/{pet.Id}");
            return get.StatusCode == HttpStatusCode.NotFound;
        });

        Assert.True(deleted, "После DELETE GET /pet/{id} не вернул 404");
    }

    [Theory]
    [InlineData(9223372036854775807)]
    [InlineData(9223372036854775806)]
    [InlineData(9000000000000000001)]
    [InlineData(9000000000000000002)]
    [InlineData(9000000000000000003)]
    [InlineData(9000000000000000004)]
    [InlineData(9000000000000000005)]
    [InlineData(9000000000000000006)]
    [InlineData(9000000000000000007)]
    public async Task Pet_GetById_NonExisting_ReturnsNotFound(long id)
    {
        (await _api.Send(HttpMethod.Delete, $"/pet/{id}")).Dispose();

        using var res = await _api.Send(HttpMethod.Get, $"/pet/{id}");
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    [Theory]
    [InlineData("not-a-status")]
    [InlineData("UNKNOWN")]
    [InlineData("")]
    public async Task Pet_FindByStatus_InvalidStatus_ReturnsArray(string status)
    {
        using var res = await _api.Send(HttpMethod.Get, $"/pet/findByStatus?status={Uri.EscapeDataString(status)}");

        if (res.StatusCode == HttpStatusCode.BadRequest) return;

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.NotNull(await _api.Read<List<Pet>>(res));
    }


    [Fact]
    public async Task Store_PlaceOrder_ReturnsOrder()
    {
        var order = NewOrder();
        using var res = await _api.Send(HttpMethod.Post, "/store/order", order);

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var created = await _api.Read<Order>(res);
        Assert.NotNull(created);
        Assert.Equal(order.Id, created!.Id);
    }

    [Fact]
    public async Task Store_GetOrderById_Existing_ReturnsOrder()
    {
        var order = await CreateOrderAsync();
        using var res = await _api.Send(HttpMethod.Get, $"/store/order/{order.Id}");

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var found = await _api.Read<Order>(res);
        Assert.Equal(order.Id, found?.Id);
    }

    [Fact]
    public async Task Store_DeleteOrder_ThenGetReturnsNotFound()
    {
        var order = await CreateOrderAsync();
        (await _api.Send(HttpMethod.Delete, $"/store/order/{order.Id}")).Dispose();

        var deleted = await Api.Eventually(async () =>
        {
            using var get = await _api.Send(HttpMethod.Get, $"/store/order/{order.Id}");
            return get.StatusCode == HttpStatusCode.NotFound;
        });

        Assert.True(deleted, "После DELETE заказ всё ещё доступен");
    }

    [Fact]
    public async Task Store_GetInventory_ReturnsMap()
    {
        using var res = await _api.Send(HttpMethod.Get, "/store/inventory");

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.NotNull(await _api.Read<Dictionary<string, int>>(res));
    }

    [Theory]
    [InlineData(999999901)]
    [InlineData(999999902)]
    [InlineData(999999903)]
    [InlineData(999999904)]
    [InlineData(999999905)]
    [InlineData(999999906)]
    [InlineData(999999907)]
    public async Task Store_GetOrderById_NonExisting_ReturnsNotFound(long id)
    {
        (await _api.Send(HttpMethod.Delete, $"/store/order/{id}")).Dispose();

        using var res = await _api.Send(HttpMethod.Get, $"/store/order/{id}");
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }


    [Fact]
    public async Task User_Create_ThenGetReturnsUser()
    {
        var user = NewUser();
        (await _api.Send(HttpMethod.Post, "/user", user)).Dispose();

        var exists = await Api.Eventually(async () =>
        {
            using var get = await _api.Send(HttpMethod.Get, $"/user/{user.Username}");
            return get.StatusCode == HttpStatusCode.OK;
        });

        Assert.True(exists, $"Пользователь {user.Username} не найден после создания");
    }

    [Fact]
    public async Task User_Update_ChangesEmail()
    {
        var user = NewUser();
        (await _api.Send(HttpMethod.Post, "/user", user)).Dispose();

        var updated = user with { Email = "new_" + user.Email };
        (await _api.Send(HttpMethod.Put, $"/user/{user.Username}", updated)).Dispose();

        var changed = await Api.Eventually(async () =>
        {
            using var get = await _api.Send(HttpMethod.Get, $"/user/{user.Username}");
            return (await _api.Read<User>(get))?.Email == updated.Email;
        });

        Assert.True(changed, "Email пользователя не обновился");
    }

    [Fact]
    public async Task User_Delete_ThenGetReturnsNotFound()
    {
        var user = NewUser();
        (await _api.Send(HttpMethod.Post, "/user", user)).Dispose();
        (await _api.Send(HttpMethod.Delete, $"/user/{user.Username}")).Dispose();

        var deleted = await Api.Eventually(async () =>
        {
            using var get = await _api.Send(HttpMethod.Get, $"/user/{user.Username}");
            return get.StatusCode == HttpStatusCode.NotFound;
        });

        Assert.True(deleted, "После DELETE GET /user/{username} не вернул 404");
    }

    [Fact]
    public async Task User_CreateWithList_ReturnsOk()
    {
        var users = new[] { NewUser(), NewUser() };
        using var res = await _api.Send(HttpMethod.Post, "/user/createWithList", users);

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }

    [Theory]
    [InlineData("no_such_user_9001")]
    [InlineData("no_such_user_9002")]
    [InlineData("no_such_user_9003")]
    [InlineData("no_such_user_9004")]
    [InlineData("no_such_user_9005")]
    [InlineData("no_such_user_9006")]
    [InlineData("no_such_user_9007")]
    public async Task User_GetByName_NonExisting_ReturnsNotFound(string username)
    {
        (await _api.Send(HttpMethod.Delete, $"/user/{username}")).Dispose();

        using var res = await _api.Send(HttpMethod.Get, $"/user/{username}");
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }


    private static long NextId() => 9_000_000_000_000_000L + Random.Shared.Next(1_000_000);

    private static Pet NewPet() =>
        new(NextId(), "pet-" + Guid.NewGuid().ToString("N")[..8], "available",
            new[] { "https://example.com/pet.png" });

    private static Order NewOrder() =>
        new(Random.Shared.Next(1_000_000_000, 2_000_000_000), NextId(), 1, DateTime.UtcNow, "placed", false);

    private static User NewUser() =>
        new(Random.Shared.Next(1_000_000_000, 2_000_000_000),
            "user_" + Guid.NewGuid().ToString("N")[..10],
            $"{Guid.NewGuid():N}@mail.com",
            "qwerty123", "+70000000000", "Ivan", "Petrov", 1);

    private async Task<Pet> CreatePetAsync()
    {
        var pet = NewPet();
        using var res = await _api.Send(HttpMethod.Post, "/pet", pet);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        return pet;
    }

    private async Task<Order> CreateOrderAsync()
    {
        var order = NewOrder();
        using var res = await _api.Send(HttpMethod.Post, "/store/order", order);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        return order;
    }
}


public record Pet(long Id, string Name, string Status, string[]? PhotoUrls);
public record Order(long Id, long PetId, int Quantity, DateTime ShipDate, string Status, bool Complete);
public record User(long Id, string Username, string Email, string Password,
                   string Phone, string FirstName, string LastName, int UserStatus);


public sealed class Api : IDisposable
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly HttpClient _http = new()
    {
        BaseAddress = new Uri("https://petstore.swagger.io/v2/"),
        Timeout = TimeSpan.FromSeconds(30)
    };

    public Api()
    {
        _http.DefaultRequestHeaders.Add("api_key", "special-key");
        _http.DefaultRequestHeaders.Add("Accept", "application/json");
    }

    public async Task<HttpResponseMessage> Send(HttpMethod method, string path, object? body = null)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                using var request = new HttpRequestMessage(method, path.TrimStart('/'));
                if (body is not null) request.Content = JsonContent.Create(body, options: Json);

                var response = await _http.SendAsync(request);

                if (response.StatusCode == HttpStatusCode.NotFound &&
                    response.Content.Headers.ContentType?.MediaType == "text/html")
                {
                    throw new InvalidOperationException(
                        $"Пришла HTML-страница 404 вместо JSON — проверь адрес: {request.RequestUri}");
                }

                if ((int)response.StatusCode < 500 || attempt == 4) return response;
                response.Dispose();
            }
            catch (HttpRequestException) when (attempt < 4) { }
            catch (TaskCanceledException) when (attempt < 4) { }

            await Task.Delay(400 * attempt);
        }
    }

    public async Task<T?> Read<T>(HttpResponseMessage response)
    {
        var json = await response.Content.ReadAsStringAsync();
        return string.IsNullOrWhiteSpace(json) ? default : JsonSerializer.Deserialize<T>(json, Json);
    }

    public static async Task<bool> Eventually(Func<Task<bool>> condition, int seconds = 10)
    {
        var until = DateTime.UtcNow.AddSeconds(seconds);
        while (DateTime.UtcNow < until)
        {
            if (await condition()) return true;
            await Task.Delay(500);
        }
        return false;
    }

    public void Dispose() => _http.Dispose();
}
