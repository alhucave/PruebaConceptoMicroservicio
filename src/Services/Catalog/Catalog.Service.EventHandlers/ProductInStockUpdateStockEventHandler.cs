using Catalog.Domain;
using Catalog.Persistence.Database;
using Catalog.Service.EventHandlers.Commands;
using Catalog.Service.EventHandlers.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using static Catalog.Common.Enums;

namespace Catalog.Service.EventHandlers
{
    public class ProductInStockUpdateStockEventHandler :
        INotificationHandler<ProductInStockUpdateStockCommand>
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<ProductInStockUpdateStockEventHandler> _logger;
        private static bool _falla = false;
        private static readonly Random Random = new Random();

        public ProductInStockUpdateStockEventHandler(
            ApplicationDbContext context,
            ILogger<ProductInStockUpdateStockEventHandler> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task Handle(ProductInStockUpdateStockCommand notification, CancellationToken cancellationToken)
        {
            //Código para generar fallas aleatorias 
            var ran = Random.Next(1, 2);
            _logger.LogInformation($"--- Ramdom" + ran.ToString());
            if (ran == 1)
            {
                _falla = true;
            }

            if (_falla)
            {
                _falla = false;
                throw new Exception("Courrio un error");

            }


            //_logger.LogInformation("--- ProductInStockUpdateStockCommand started");

            var products = notification.Items.Select(x => x.ProductId);
            var stocks = await _context.Stocks.Where(x => products.Contains(x.ProductId)).ToListAsync();

            //_logger.LogInformation("--- Retrieve products from database");

            foreach (var item in notification.Items) 
            {
                var entry = stocks.SingleOrDefault(x => x.ProductId == item.ProductId);

                if (item.Action == ProductInStockAction.Substract)
                {
                    if (entry == null || item.Stock > entry.Stock)
                    {
                        _logger.LogError($"--- Product {entry.ProductId} -doens't have enough stock");
                        throw new ProductInStockUpdateStockCommandException($"Product {entry.ProductId} - doens't have enough stock");
                    }

                    entry.Stock -= item.Stock;

                    _logger.LogInformation($"--- Product {entry.ProductId} - its stock was subtracted and its new stock is {entry.Stock}");
                }
                else
                {
                    if (entry == null)
                    {
                        entry = new ProductInStock
                        {
                            ProductId = item.ProductId
                        };

                        _logger.LogInformation($"--- New stock record was created for {entry.ProductId} because didn't exists before");

                        await _context.AddAsync(entry);
                    }

                    _logger.LogInformation($"--- Add stock to product {entry.ProductId}");
                    entry.Stock += item.Stock;
                }
            }

            await _context.SaveChangesAsync();
        }
    }
}
