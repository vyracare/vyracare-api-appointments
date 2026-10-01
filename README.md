# Vyracare API Appointments

API responsável pela agenda clínica e pelos indicadores operacionais do dashboard.

## Endpoints

- `GET /api/appointments`
- `GET /api/appointments/{id}`
- `POST /api/appointments`
- `PATCH /api/appointments/{id}/status`
- `GET /api/appointments/dashboard/summary`
- `GET /health`

Todos os endpoints, exceto `health`, exigem o JWT emitido pela API de autenticação.

## Indicadores

- atendimentos de hoje: agendamentos do dia que não estejam cancelados;
- confirmados nas últimas duas horas: agendamentos de hoje cujo `confirmedAt` esteja na janela;
- retornos pendentes: `followUpDueAt` nos próximos dias sem `followUpScheduledAt`;
- ocupação semanal: minutos agendados não cancelados divididos por `Appointments:WeeklyAvailableMinutes`.

Datas são persistidas em UTC e os limites de dia/semana usam `Appointments:TimeZone`.

## Execução local

Configure `MONGO_PARAMETER_NAME`, `JWT_PARAMETER_NAME`, `Mongo__Database` e execute:

```powershell
$env:ASPNETCORE_URLS = 'http://localhost:5003'
dotnet run --no-launch-profile
```

Swagger: `http://localhost:5003/swagger`
