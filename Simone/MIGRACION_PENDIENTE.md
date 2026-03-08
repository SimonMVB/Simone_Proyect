# Migraciones pendientes

Ejecuta estos comandos en la **Package Manager Console** de Visual Studio
(o en terminal con `dotnet ef`):

```
Add-Migration ReservaStockYPerfilTienda
Update-Database
```

Esta migración agrega:
- Columnas de perfil público al table `Vendedores` (Slug, Bio, BannerPath, Verificado, InstagramUrl, TikTokUrl, FacebookUrl)
- Nueva tabla `ReservasStock` para reserva de stock en el carrito POS
