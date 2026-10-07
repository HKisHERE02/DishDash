FROM node:24-alpine AS frontend-build

WORKDIR /src/frontend

COPY frontend/package.json frontend/package-lock.json ./
RUN npm ci

COPY frontend/ ./
RUN npm run build


FROM mcr.microsoft.com/dotnet/sdk:10.0 AS backend-build

WORKDIR /src

COPY . .

RUN dotnet restore backend/src/DishDash.Api/DishDash.Api.csproj

RUN dotnet publish backend/src/DishDash.Api/DishDash.Api.csproj \
    -c Release \
    -o /app/publish \
    --no-restore


FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final

WORKDIR /app

COPY --from=backend-build /app/publish ./
COPY --from=frontend-build /src/frontend/dist ./wwwroot

ENV ASPNETCORE_ENVIRONMENT=Production

EXPOSE 10000

CMD ["sh", "-c", "dotnet DishDash.Api.dll --urls http://0.0.0.0:${PORT:-10000}"]