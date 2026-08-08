-- Stateful REST profile for the Pick execution path.
--
-- amount=5001 is intentionally above the default MaxBet (5000). The request
-- still traverses REST -> gRPC -> game execution and persistence reads, while
-- PickAction rejects it before producing wallet/economy effects. This keeps a
-- load test from draining the development user's balance.

local setup_thread_id = 0

function setup(thread)
  setup_thread_id = setup_thread_id + 1
  thread:set("profile_thread_id", setup_thread_id)
end

function init()
  counter = 0
  thread_id = tonumber(wrk.thread:get("profile_thread_id")) or 1
  local amount = tonumber(os.getenv("PICK_AMOUNT") or "5001")
  load_user_count = tonumber(os.getenv("LOAD_USER_COUNT") or "0")
  load_user_base = tonumber(os.getenv("LOAD_USER_BASE") or "1000000")
  log_first_error = os.getenv("LOG_FIRST_ERROR") == "1"
  first_error_logged = false
  body = string.format('{"amount":%d,"variants":["a","b"],"backedIndices":[0]}', amount)
  headers = {
    ["Content-Type"] = "application/json",
    ["Host"] = os.getenv("REST_HOST") or "api.casinoshiz.localhost"
  }

  local token = os.getenv("REST_DEV_TOKEN")
  if token then
    headers["Authorization"] = "Bearer " .. token
  end
end

request = function()
  counter = counter + 1
  headers["Idempotency-Key"] = "pick-profile-" .. thread_id .. "-" .. counter
  if load_user_count > 0 then
    local user_id = load_user_base + ((thread_id - 1) * load_user_count) + ((counter - 1) % load_user_count)
    headers["X-Load-Test-User-Id"] = tostring(user_id)
  end
  return wrk.format(
    "POST",
    "/api/v1/tenants/e2e/scopes/42/pick",
    headers,
    body)
end

response = function(status, _, body)
  if log_first_error and status >= 400 and not first_error_logged then
    first_error_logged = true
    io.stderr:write(string.format("first HTTP %d response: %s\n", status, body))
  end
end
