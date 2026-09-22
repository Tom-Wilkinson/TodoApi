using System.Collections.Concurrent;

var builder = WebApplication.CreateBuilder(args);

//builder.Services.AddOpenApi();
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll",
        policy =>
        {
            policy.AllowAnyOrigin()
                .AllowAnyHeader()
                .AllowAnyMethod();
        });
});

var app = builder.Build();

app.UseCors("AllowAll");


if (app.Environment.IsDevelopment())
{
    //app.MapOpenApi();
}

var todos = new ConcurrentDictionary<Guid, TodoItem>();

app.MapPost("/api/todos", (TodoRequest request) =>
{
    if (string.IsNullOrWhiteSpace(request.Title))
    {
        return Results.BadRequest(new { error = "Title is required." });
    }

    if (request.DueDate < DateOnly.FromDateTime(DateTime.Today))
    {
        return Results.BadRequest(new { error = "Due date cannot be in the past." });
    }

    var todo = new TodoItem
    {
        Id = Guid.NewGuid(),
        Title = request.Title,
        IsCompleted = request.IsCompleted,
        DueDate = request.DueDate
    };

    todos[todo.Id] = todo;

    return Results.Created($"/api/todos/{todo.Id}", todo);
});

app.MapGet("/api/todos", (int page = 1, int pageSize = 2) =>
{
    var allTodos = todos.Values.ToList();

    var pagedItems = allTodos
        .Skip((page - 1) * pageSize)
        .Take(pageSize)
        .ToList();

    var result = new
    {
        items = pagedItems,
        count = allTodos.Count,
        page = page,
        pageSize = pageSize,
        hasNext = page * pageSize < allTodos.Count
    };

    return Results.Ok(result);
});

app.MapGet("/api/todos/{id:guid}", (Guid id) =>
{
    if (!todos.TryGetValue(id, out var todo))
    {
        return Results.NotFound();
    }

    return Results.Ok(todo);
});

app.MapPut("/api/todos/{id:guid}", (Guid id, TodoRequest request) =>
{
    if (!todos.ContainsKey(id))
    {
        return Results.NotFound();
    }

    if (request.IsCompleted &&
        request.DueDate > DateOnly.FromDateTime(DateTime.Today))
    {
        return Results.Conflict(new
        {
            error = "A todo with a future due date cannot be completed."
        });
    }

    var updatedTodo = new TodoItem
    {
        Id = id,
        Title = request.Title,
        IsCompleted = request.IsCompleted,
        DueDate = request.DueDate
    };

    todos[id] = updatedTodo;

    return Results.Ok(updatedTodo);
});

app.MapDelete("/api/todos/{id:guid}", (Guid id) =>
{
    if (!todos.TryRemove(id, out _))
    {
        return Results.NotFound();
    }

    return Results.NoContent();
});

app.Run();

public class TodoItem
{
    public Guid Id { get; set; }
    public string Title { get; set; } = "";
    public bool IsCompleted { get; set; }
    public DateOnly DueDate { get; set; }
}

public class TodoRequest
{
    public string Title { get; set; } = "";
    public bool IsCompleted { get; set; }
    public DateOnly DueDate { get; set; }
}