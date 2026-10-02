using DishDash.IntegrationTests;
using var factory = new AppFactory();
factory.UseKestrel(5142);
await factory.Initialize();
Console.WriteLine("DishDash browser test API ready at http://127.0.0.1:5142");
await Task.Delay(Timeout.Infinite);
