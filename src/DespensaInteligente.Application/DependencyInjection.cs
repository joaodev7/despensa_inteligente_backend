using Microsoft.Extensions.DependencyInjection;
using FluentValidation;
using DespensaInteligente.Application.Common.Interfaces;
using DespensaInteligente.Application.Services;
using DespensaInteligente.Application.InvoiceScanner.Interfaces;
using DespensaInteligente.Application.InvoiceScanner.Services;
using DespensaInteligente.Application.InvoiceScanner.Validators;
using DespensaInteligente.Application.InvoiceScanner.DTOs;

namespace DespensaInteligente.Application
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddApplicationServices(this IServiceCollection services)
        {
            services.AddScoped<IAuthService, AuthService>();
            services.AddScoped<ICategoriaService, CategoriaService>();
            services.AddScoped<IItemService, ItemService>();
            services.AddScoped<ICompraService, CompraService>();
            services.AddScoped<IListaManualService, ListaManualService>();
            services.AddScoped<ILoteService, LoteService>();
            services.AddScoped<INfeService, NfeService>();
            services.AddScoped<IDashboardService, DashboardService>();

            // Module: InvoiceScanner
            services.AddScoped<IInvoiceScanService, InvoiceScanService>();
            services.AddScoped<IValidator<ScanInvoiceRequestDto>, ScanInvoiceRequestValidator>();

            return services;
        }
    }
}
