FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY BusBooking.Api.csproj ./
RUN dotnet restore
COPY . ./
RUN dotnet publish BusBooking.Api.csproj --configuration Release --no-restore --output /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080 \
    JsonDatabase__FilePath="/var/data/busbooking.json"
EXPOSE 8080
VOLUME ["/var/data"]

COPY --from=build /app/publish ./
ENTRYPOINT ["dotnet", "BusBooking.Api.dll"]
