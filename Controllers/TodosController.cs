using Azure.Messaging.ServiceBus;
using DotNetCoreSqlDb.Data;
using DotNetCoreSqlDb.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DotNetCoreSqlDb.Controllers
{
    public class TodosController : Controller
    {
        private readonly ILogger<TodosController> _logger;
        private readonly MyDatabaseContext _context;
        private readonly ServiceBusSender _todoSender;

        public TodosController(MyDatabaseContext context, ServiceBusSender todoSender, ILogger<TodosController> logger)
        {
            _context = context;
            _todoSender = todoSender;
            _logger = logger;
        }

        // GET: Todos
        public async Task<IActionResult> Index()
        {
            return View(await BuildIndexViewModel());
        }

        // POST: Todos/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(
            [Bind("Description", Prefix = "NewTodo")] Todo todo)
        {
            if (ModelState.IsValid)
            {
                _context.Add(todo);
                await _context.SaveChangesAsync();
                await PublishTodoCreatedAsync(todo);

                return RedirectToAction(nameof(Index));
            }

            return View(nameof(Index), await BuildIndexViewModel(todo));
        }

        // POST: Todos/Delete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var todo = await _context.Todo.FindAsync(id);
            if (todo == null)
            {
                return NotFound();
            }

            _context.Todo.Remove(todo);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        private async Task<TodosIndexViewModel> BuildIndexViewModel(Todo? newTodo = null)
        {
            _logger.LogInformation("Data from database.");
            return new TodosIndexViewModel
            {
                NewTodo = newTodo ?? new Todo(),
                Todos = await _context.Todo.AsNoTracking().ToListAsync()
            };
        }

        private async Task PublishTodoCreatedAsync(Todo todo)
        {
            var payload = new TodoCreatedMessage(todo.ID, todo.Description, DateTimeOffset.UtcNow);
            var message = new ServiceBusMessage(BinaryData.FromObjectAsJson(payload))
            {
                ContentType = "application/json",
                Subject = "TodoCreated",                // message type, useful for routing and filtering
                MessageId = $"todo-created-{todo.ID}"   // stable ID so the worker can detect duplicates
            };

            try
            {
                await _todoSender.SendMessageAsync(message);
                _logger.LogInformation("Sent TodoCreated message for todo {TodoId}.", todo.ID);
            }
            catch (Exception ex)
            {
                // The todo is already saved, so don't fail the request just because the message couldn't be sent.
                _logger.LogError(ex, "Failed to send TodoCreated message for todo {TodoId}.", todo.ID);
            }
        }
    }
}
