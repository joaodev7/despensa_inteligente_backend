using Microsoft.Extensions.DependencyInjection;
using DespensaInteligente.Application.Common.Interfaces;
using DespensaInteligente.Application.Services;

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

            return services;
        }
    }
}
