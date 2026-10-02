# Vyracare API Appointments

API responsável pela agenda clínica e pelos indicadores operacionais do dashboard.

## Endpoints

- `GET /api/appointments`
- `GET /api/appointments/{id}`
- `POST /api/appointments`
- `PATCH /api/appointments/{id}/status`
- `GET /api/appointments/dashboard/summary`
- `GET /api/appointments/notifications/due`
- `POST /api/appointments/{id}/notifications/acknowledge`
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

## Agenda e notificacoes

O cadastro aceita snapshots do nome e telefone do paciente, profissional e procedimento. O lembrete e definido por quantidade e unidade (`Hours` ou `Days`) e convertido para `reminderAt` em UTC.

O frontend consulta notificacoes vencidas periodicamente, exibe uma notificacao nativa do navegador e confirma a entrega pelo endpoint de acknowledgement. Agendamentos concluidos ou cancelados nao geram notificacao.
