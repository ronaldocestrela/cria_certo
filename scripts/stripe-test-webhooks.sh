#!/usr/bin/env bash
# ==============================================================================
# CriaCerto - Script de Homologação e Testes de Webhooks com Stripe CLI
# ==============================================================================
# Propósito:
#   Facilitar o encaminhamento e simulação de eventos de cobrança e ciclo de vida
#   do Stripe para a API local do CriaCerto.
#
# Pré-requisitos:
#   - Stripe CLI instalado (stripe version >= 1.14)
#   - Autenticado via: stripe login
#   - API CriaCerto rodando (padrão: http://localhost:5000)
# ==============================================================================

set -euo pipefail

# Cores para saída no terminal
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
CYAN='\033[0;36m'
BOLD='\033[1m'
NC='\033[0m' # No Color

DEFAULT_WEBHOOK_URL="http://localhost:5000/api/v1/payments/stripe-webhook"

print_header() {
    echo -e "${BLUE}${BOLD}===================================================================${NC}"
    echo -e "${BLUE}${BOLD}   CriaCerto SaaS - Utilitário de Testes e Webhooks Stripe CLI    ${NC}"
    echo -e "${BLUE}${BOLD}===================================================================${NC}"
}

check_dependencies() {
    if ! command -v stripe &> /dev/null; then
        echo -e "${RED}[ERRO] Stripe CLI não encontrado no PATH.${NC}"
        echo -e "${YELLOW}Instale via curl ou gerenciador de pacotes:${NC}"
        echo -e "  Linux/WSL: curl -s https://packages.stripe.dev/api/security/keypair/stripe-cli-gpg/public | gpg --dearmor | sudo tee /usr/share/keyrings/stripe.gpg"
        echo -e "             echo \"deb [signed-by=/usr/share/keyrings/stripe.gpg] https://packages.stripe.dev/stripe-cli-debian-local stable main\" | sudo tee /etc/apt/sources.list.d/stripe.list"
        echo -e "             sudo apt update && sudo apt install stripe"
        echo -e "  Ou via Homebrew / Scoop / npm / binário direto."
        exit 1
    fi
}

usage() {
    print_header
    echo -e "${BOLD}Uso:${NC} $0 [COMANDO] [OPÇÕES]"
    echo ""
    echo -e "${BOLD}Comandos disponíveis:${NC}"
    echo -e "  ${CYAN}listen [URL]${NC}               Inicia a escuta e encaminhamento local via 'stripe listen'"
    echo -e "                              (Default URL: ${DEFAULT_WEBHOOK_URL})"
    echo -e "  ${CYAN}trigger-all${NC}                Dispara em sequência os 4 eventos principais do ciclo de vida:"
    echo -e "                              1. checkout.session.completed"
    echo -e "                              2. invoice.paid"
    echo -e "                              3. invoice.payment_failed"
    echo -e "                              4. customer.subscription.deleted"
    echo -e "  ${CYAN}trigger <EVENT_NAME>${NC}       Dispara um evento específico pelo nome"
    echo -e "  ${CYAN}check-api [URL]${NC}            Verifica a conectividade HTTP com o endpoint de webhook local"
    echo -e "  ${CYAN}help${NC}                       Exibe esta tela de ajuda"
    echo ""
    echo -e "${BOLD}Exemplos:${NC}"
    echo -e "  $0 listen"
    echo -e "  $0 trigger-all"
    echo -e "  $0 trigger checkout.session.completed"
    echo -e "  $0 trigger invoice.paid"
    echo ""
}

check_api_endpoint() {
    local url="${1:-$DEFAULT_WEBHOOK_URL}"
    echo -e "${CYAN}[INFO] Verificando conectividade com o endpoint:${NC} ${url}"
    
    # Envia POST sem corpo/assinatura para checar se a API está online (espera-se HTTP 400 por assinatura ausente)
    local http_code
    http_code=$(curl -s -o /dev/null -w "%{http_code}" -X POST "${url}" || echo "000")
    
    if [ "$http_code" = "400" ]; then
        echo -e "${GREEN}[OK] API está online e rejeitou requisição não assinada conforme esperado (HTTP 400).${NC}"
        return 0
    elif [ "$http_code" = "000" ]; then
        echo -e "${YELLOW}[AVISO] Não foi possível conectar a ${url}. Certifique-se de que a API está rodando ('dotnet run --project src/Host/CriaCerto.Api').${NC}"
        return 1
    else
        echo -e "${YELLOW}[INFO] Resposta recebida da API: HTTP ${http_code}.${NC}"
        return 0
    fi
}

start_listener() {
    local url="${1:-$DEFAULT_WEBHOOK_URL}"
    check_dependencies
    print_header
    echo -e "${CYAN}[INFO] Iniciando encaminhamento de webhooks para:${NC} ${url}"
    echo -e "${YELLOW}[DICA] Copie o 'webhook signing secret' exibido abaixo (whsec_...) e configure em:${NC}"
    echo -e "       STRIPE_WEBHOOK_SECRET no seu arquivo .env ou appsettings.Development.json"
    echo -e "${BLUE}-------------------------------------------------------------------${NC}"
    
    exec stripe listen --forward-to "${url}"
}

trigger_single_event() {
    local event="$1"
    check_dependencies
    echo -e "${CYAN}[DISPARO] Disparando evento Stripe:${NC} ${BOLD}${event}${NC}"
    stripe trigger "${event}"
    echo -e "${GREEN}[SUCESSO] Evento '${event}' emitido com sucesso.${NC}"
}

trigger_all_events() {
    check_dependencies
    print_header
    echo -e "${CYAN}[INÍCIO] Executando homologação dos 4 eventos principais do ciclo de faturamento...${NC}"
    echo ""

    # 1. Checkout Session Completed
    echo -e "${BOLD}1/4. Disparando 'checkout.session.completed' (Ativação de Assinatura)...${NC}"
    stripe trigger checkout.session.completed
    sleep 2

    # 2. Invoice Paid
    echo -e "${BOLD}2/4. Disparando 'invoice.paid' (Renovação / Quitação de Fatura)...${NC}"
    stripe trigger invoice.paid
    sleep 2

    # 3. Invoice Payment Failed
    echo -e "${BOLD}3/4. Disparando 'invoice.payment_failed' (Inadimplência / PastDue)...${NC}"
    stripe trigger invoice.payment_failed
    sleep 2

    # 4. Customer Subscription Deleted
    echo -e "${BOLD}4/4. Disparando 'customer.subscription.deleted' (Cancelamento de Assinatura)...${NC}"
    stripe trigger customer.subscription.deleted

    echo ""
    echo -e "${GREEN}${BOLD}[CONCLUÍDO] Todos os 4 eventos foram emitidos com sucesso via Stripe CLI.${NC}"
    echo -e "${YELLOW}Verifique o log da API e a tabela 'Tenants' / 'SubscriptionHistories' para confirmar as transições atômicas.${NC}"
}

# ------------------------------------------------------------------------------
# Roteamento dos comandos CLI
# ------------------------------------------------------------------------------
COMMAND="${1:-help}"

case "$COMMAND" in
    listen)
        start_listener "${2:-$DEFAULT_WEBHOOK_URL}"
        ;;
    trigger-all)
        trigger_all_events
        ;;
    trigger)
        if [ -z "${2:-}" ]; then
            echo -e "${RED}[ERRO] Nome do evento não informado. Ex: $0 trigger checkout.session.completed${NC}"
            exit 1
        fi
        trigger_single_event "$2"
        ;;
    check-api)
        check_api_endpoint "${2:-$DEFAULT_WEBHOOK_URL}"
        ;;
    help|--help|-h)
        usage
        ;;
    *)
        echo -e "${RED}[ERRO] Comando desconhecido: '$COMMAND'${NC}"
        echo ""
        usage
        exit 1
        ;;
esac
