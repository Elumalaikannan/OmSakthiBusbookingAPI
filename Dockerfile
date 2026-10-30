FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY BusBooking.Api.csproj ./
RUN dotnet restore
COPY . ./
RUN dotnet publish BusBooking.Api.csproj --configuration Release --no-restore --output /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080 \
    ConnectionStrings__Default="Data Source=busbooking.db"
EXPOSE 8080
VOLUME ["/app/data"]

COPY --from=build /app/publish ./
ENTRYPOINT ["dotnet", "BusBooking.Api.dll"]
