const responseBox = document.getElementById("responseBox");
const apiBaseUrlInput = document.getElementById("apiBaseUrl");
const saveBaseUrlBtn = document.getElementById("saveBaseUrl");
const clearResponseBtn = document.getElementById("clearResponseBtn");
const checkApiBtn = document.getElementById("checkApiBtn");
const listProductsBtn = document.getElementById("listProductsBtn");
const orderItems = document.getElementById("orderItems");
const addItemBtn = document.getElementById("addItemBtn");
const orderItemTemplate = document.getElementById("orderItemTemplate");

const customerForm = document.getElementById("customerForm");
const paymentMethodForm = document.getElementById("paymentMethodForm");
const createOrderForm = document.getElementById("createOrderForm");
const getOrderForm = document.getElementById("getOrderForm");
const customerOrdersForm = document.getElementById("customerOrdersForm");
const neighborhoodOptions = document.getElementById("neighborhoodOptions");
const postalCodeHint = document.getElementById("postalCodeHint");
const postalCodeInput = customerForm.querySelector("input[name='postalCode']");
const neighborhoodInput = customerForm.querySelector("input[name='neighborhood']");
const municipalityInput = customerForm.querySelector("input[name='municipality']");
const stateInput = customerForm.querySelector("input[name='state']");

const STORAGE_KEY = "aiChallengeBaseUrl";

function getBaseUrl() {
  const saved = localStorage.getItem(STORAGE_KEY);
  if (saved && saved.trim()) {
    return saved.trim().replace(/\/$/, "");
  }

  return window.location.origin;
}

function setBaseUrl(url) {
  if (!url || !url.trim()) {
    localStorage.removeItem(STORAGE_KEY);
    return;
  }

  localStorage.setItem(STORAGE_KEY, url.trim().replace(/\/$/, ""));
}

function renderResponse(title, status, payload, isOk) {
  const statusClass = isOk ? "status-ok" : "status-fail";
  const prettyPayload = typeof payload === "string" ? payload : JSON.stringify(payload, null, 2);

  responseBox.innerHTML = `<span class="${statusClass}">${title} [${status}]</span>\n${prettyPayload}`;
}

async function callApi(path, options = {}) {
  const url = `${getBaseUrl()}${path}`;
  const method = options.method || "GET";
  const hasBody = options.body !== undefined;
  const silent = options.silent === true;

  try {
    const response = await fetch(url, {
      method,
      headers: {
        "Content-Type": "application/json",
        ...options.headers
      },
      body: hasBody ? JSON.stringify(options.body) : undefined
    });

    const text = await response.text();
    let data = text;

    if (text) {
      try {
        data = JSON.parse(text);
      } catch {
        data = text;
      }
    }

    if (!silent) {
      renderResponse(`${method} ${path}`, response.status, data || "(sin contenido)", response.ok);
    }

    return data;
  } catch (error) {
    if (!silent) {
      renderResponse(`${method} ${path}`, "ERROR", error.message || "Error de red", false);
    }

    throw error;
  }
}

function updateNeighborhoodOptions(addresses) {
  neighborhoodOptions.innerHTML = "";
  addresses.forEach((address) => {
    const option = document.createElement("option");
    option.value = address.neighborhood || "";
    neighborhoodOptions.appendChild(option);
  });
}

function fillAddressFields(addresses) {
  if (addresses.length === 0) {
    updateNeighborhoodOptions([]);
    postalCodeHint.textContent = "No hay coincidencias para ese codigo postal.";
    return;
  }

  updateNeighborhoodOptions(addresses);
  const first = addresses[0];
  municipalityInput.value = first.municipality || municipalityInput.value;
  stateInput.value = first.state || stateInput.value;

  const hasCurrentNeighborhood = addresses.some(
    (address) => String(address.neighborhood || "").toLowerCase() === neighborhoodInput.value.toLowerCase()
  );

  if (!hasCurrentNeighborhood) {
    neighborhoodInput.value = first.neighborhood || "";
  }

  postalCodeHint.textContent =
    addresses.length === 1
      ? "Direccion sugerida cargada automaticamente."
      : `Se encontraron ${addresses.length} colonias para este C.P.`;
}

let postalLookupTimer = null;

async function lookupAddressByPostalCode() {
  const postalCode = postalCodeInput.value.trim();
  if (!/^\d{5}$/.test(postalCode)) {
    updateNeighborhoodOptions([]);
    postalCodeHint.textContent = "Ingresa un codigo postal de 5 digitos.";
    return;
  }

  try {
    const data = await callApi(`/api/address-catalog/${encodeURIComponent(postalCode)}`, { silent: true });
    if (Array.isArray(data)) {
      fillAddressFields(data);
      return;
    }

    fillAddressFields([]);
  } catch {
    postalCodeHint.textContent = "No se pudo consultar el catalogo de direcciones.";
  }
}

function createOrderItemRow(initial = { sku: "", quantity: 1 }) {
  const fragment = orderItemTemplate.content.cloneNode(true);
  const item = fragment.querySelector(".order-item");

  const skuInput = item.querySelector("input[name='sku']");
  const quantityInput = item.querySelector("input[name='quantity']");
  const removeBtn = item.querySelector(".remove-item");

  skuInput.value = initial.sku;
  quantityInput.value = initial.quantity;

  removeBtn.addEventListener("click", () => {
    item.remove();
  });

  orderItems.appendChild(fragment);
}

function getOrderItems() {
  const items = [];
  const rows = orderItems.querySelectorAll(".order-item");

  rows.forEach((row) => {
    const sku = row.querySelector("input[name='sku']").value.trim();
    const quantity = Number.parseInt(row.querySelector("input[name='quantity']").value, 10);

    if (!sku || Number.isNaN(quantity) || quantity <= 0) {
      return;
    }

    items.push({ sku, quantity });
  });

  return items;
}

saveBaseUrlBtn.addEventListener("click", () => {
  setBaseUrl(apiBaseUrlInput.value);
  apiBaseUrlInput.value = getBaseUrl();
  renderResponse("Configuracion", 200, { baseUrl: getBaseUrl() }, true);
});

clearResponseBtn.addEventListener("click", () => {
  responseBox.textContent = "Sin resultados aun.";
});

addItemBtn.addEventListener("click", () => {
  createOrderItemRow();
});

postalCodeInput.addEventListener("input", () => {
  if (postalLookupTimer) {
    window.clearTimeout(postalLookupTimer);
  }

  postalLookupTimer = window.setTimeout(() => {
    lookupAddressByPostalCode();
  }, 300);
});

postalCodeInput.addEventListener("blur", () => {
  lookupAddressByPostalCode();
});

checkApiBtn.addEventListener("click", async () => {
  await callApi("/api-info");
});

listProductsBtn.addEventListener("click", async () => {
  await callApi("/api/products");
});

customerForm.addEventListener("submit", async (event) => {
  event.preventDefault();
  const form = new FormData(customerForm);

  const payload = {
    fullName: form.get("fullName"),
    curp: form.get("curp"),
    birthDate: form.get("birthDate"),
    address: {
      streetAndNumber: form.get("streetAndNumber"),
      neighborhood: form.get("neighborhood"),
      postalCode: form.get("postalCode"),
      municipality: form.get("municipality"),
      state: form.get("state")
    }
  };

  await callApi("/api/customers", { method: "POST", body: payload });
});

paymentMethodForm.addEventListener("submit", async (event) => {
  event.preventDefault();
  const form = new FormData(paymentMethodForm);

  const payload = {
    customerId: form.get("customerId"),
    cardNumber: form.get("cardNumber"),
    cardType: Number.parseInt(form.get("cardType"), 10),
    cardholderName: form.get("cardholderName"),
    expiration: form.get("expiration"),
    cvv: form.get("cvv")
  };

  await callApi("/api/payment-methods", { method: "POST", body: payload });
});

createOrderForm.addEventListener("submit", async (event) => {
  event.preventDefault();
  const form = new FormData(createOrderForm);

  const payload = {
    customerId: form.get("customerId"),
    paymentMethodId: form.get("paymentMethodId"),
    products: getOrderItems()
  };

  if (payload.products.length === 0) {
    renderResponse("Validacion", 400, "Agrega al menos un producto a la orden.", false);
    return;
  }

  await callApi("/api/orders", { method: "POST", body: payload });
});

getOrderForm.addEventListener("submit", async (event) => {
  event.preventDefault();
  const form = new FormData(getOrderForm);
  const orderId = String(form.get("orderId") || "").trim();

  if (!orderId) {
    renderResponse("Validacion", 400, "Order ID es obligatorio.", false);
    return;
  }

  await callApi(`/api/orders/${encodeURIComponent(orderId)}`);
});

customerOrdersForm.addEventListener("submit", async (event) => {
  event.preventDefault();
  const form = new FormData(customerOrdersForm);
  const customerId = String(form.get("customerId") || "").trim();

  if (!customerId) {
    renderResponse("Validacion", 400, "Customer ID es obligatorio.", false);
    return;
  }

  await callApi(`/api/customers/${encodeURIComponent(customerId)}/orders`);
});

function initialize() {
  apiBaseUrlInput.value = getBaseUrl();

  if (orderItems.children.length === 0) {
    createOrderItemRow();
  }
}

initialize();
