#!/bin/bash
# CMDB Seed Script - Creates 20 entity types and 50 objects each via web surface
BASE="https://localhost:5001"
COOKIES="/tmp/cookies.txt"
CURL="curl.exe -sk -b $COOKIES -c $COOKIES"

# Helper: create table
create_table() {
    $CURL -H "Content-Type: application/x-www-form-urlencoded" -X POST "$BASE/create_table" -d "table_name=$1" -o /dev/null -w "%{http_code}" 2>/dev/null
    echo " Created table: $1"
}

# Helper: add column
add_column() {
    $CURL -H "Content-Type: application/x-www-form-urlencoded" -X POST "$BASE/add_column/$1" -d "column_name=$2&column_type=$3" -o /dev/null -w "%{http_code}" 2>/dev/null
    echo " Added column: $1.$2 ($3)"
}

# Helper: add FK reference
add_reference() {
    $CURL -H "Content-Type: application/x-www-form-urlencoded" -X POST "$BASE/add_reference/$1" -d "ref_table=$2" -o /dev/null -w "%{http_code}" 2>/dev/null
    echo " Added reference: $1 -> $2"
}

# Helper: create record
create_record() {
    local table=$1
    shift
    $CURL -H "Content-Type: application/x-www-form-urlencoded" -X POST "$BASE/entity/$table/create" -d "$@" -o /dev/null -w "" 2>/dev/null
}

echo "========================================="
echo "CMDB SEED SCRIPT"
echo "========================================="

# ---- PHASE 1: Create all 20 tables ----
echo ""
echo "--- Phase 1: Creating 20 entity types ---"

# Base/reference tables (no FKs to other custom tables)
create_table "LOCATION"
create_table "VENDOR"
create_table "OPERATING_SYSTEM"
create_table "ENVIRONMENT"
create_table "DEPARTMENT"
create_table "CONTACT"

# Mid-level tables
create_table "RACK"
create_table "NETWORK"
create_table "APPLICATION"
create_table "CONTRACT"
create_table "PROJECT"

# Infrastructure tables
create_table "SERVER"
create_table "DATABASE_INSTANCE"
create_table "STORAGE"
create_table "FIREWALL"

# Service tables
create_table "SERVICE"
create_table "INCIDENT"

# Junction tables (M2M)
create_table "SERVER_APPLICATION"
create_table "SERVER_NETWORK"
create_table "APPLICATION_CONTACT"

echo ""
echo "--- Phase 2: Adding columns ---"

# LOCATION columns
add_column "LOCATION" "ADDRESS" "VARCHAR2(200)"
add_column "LOCATION" "CITY" "VARCHAR2(200)"
add_column "LOCATION" "COUNTRY" "VARCHAR2(200)"
add_column "LOCATION" "TYPE" "VARCHAR2(200)"

# VENDOR columns
add_column "VENDOR" "WEBSITE" "VARCHAR2(200)"
add_column "VENDOR" "PHONE" "VARCHAR2(200)"
add_column "VENDOR" "TYPE" "VARCHAR2(200)"
add_column "VENDOR" "STATUS" "VARCHAR2(200)"

# OPERATING_SYSTEM columns
add_column "OPERATING_SYSTEM" "VERSION" "VARCHAR2(200)"
add_column "OPERATING_SYSTEM" "FAMILY" "VARCHAR2(200)"
add_column "OPERATING_SYSTEM" "ARCH" "VARCHAR2(200)"
add_column "OPERATING_SYSTEM" "END_OF_LIFE" "DATE"

# ENVIRONMENT columns
add_column "ENVIRONMENT" "TIER" "VARCHAR2(200)"
add_column "ENVIRONMENT" "DESCRIPTION" "VARCHAR2(200)"

# DEPARTMENT columns
add_column "DEPARTMENT" "CODE" "VARCHAR2(200)"
add_column "DEPARTMENT" "MANAGER" "VARCHAR2(200)"
add_column "DEPARTMENT" "COST_CENTER" "VARCHAR2(200)"

# CONTACT columns
add_column "CONTACT" "EMAIL" "VARCHAR2(200)"
add_column "CONTACT" "PHONE" "VARCHAR2(200)"
add_column "CONTACT" "ROLE" "VARCHAR2(200)"
add_column "CONTACT" "STATUS" "VARCHAR2(200)"

# RACK columns
add_column "RACK" "CAPACITY" "NUMBER"
add_column "RACK" "POWER_KW" "NUMBER"
add_column "RACK" "STATUS" "VARCHAR2(200)"

# NETWORK columns
add_column "NETWORK" "CIDR" "VARCHAR2(200)"
add_column "NETWORK" "VLAN" "NUMBER"
add_column "NETWORK" "TYPE" "VARCHAR2(200)"

# APPLICATION columns
add_column "APPLICATION" "VERSION" "VARCHAR2(200)"
add_column "APPLICATION" "STATUS" "VARCHAR2(200)"
add_column "APPLICATION" "CRITICALITY" "VARCHAR2(200)"
add_column "APPLICATION" "URL" "VARCHAR2(200)"

# CONTRACT columns
add_column "CONTRACT" "START_DATE" "DATE"
add_column "CONTRACT" "END_DATE" "DATE"
add_column "CONTRACT" "VALUE" "NUMBER"
add_column "CONTRACT" "STATUS" "VARCHAR2(200)"
add_column "CONTRACT" "TYPE" "VARCHAR2(200)"

# PROJECT columns
add_column "PROJECT" "STATUS" "VARCHAR2(200)"
add_column "PROJECT" "START_DATE" "DATE"
add_column "PROJECT" "END_DATE" "DATE"
add_column "PROJECT" "BUDGET" "NUMBER"

# SERVER columns
add_column "SERVER" "HOSTNAME" "VARCHAR2(200)"
add_column "SERVER" "IP_ADDRESS" "VARCHAR2(200)"
add_column "SERVER" "CPU_CORES" "NUMBER"
add_column "SERVER" "RAM_GB" "NUMBER"
add_column "SERVER" "STATUS" "VARCHAR2(200)"
add_column "SERVER" "TYPE" "VARCHAR2(200)"

# DATABASE_INSTANCE columns
add_column "DATABASE_INSTANCE" "ENGINE" "VARCHAR2(200)"
add_column "DATABASE_INSTANCE" "VERSION" "VARCHAR2(200)"
add_column "DATABASE_INSTANCE" "PORT" "NUMBER"
add_column "DATABASE_INSTANCE" "SIZE_GB" "NUMBER"
add_column "DATABASE_INSTANCE" "STATUS" "VARCHAR2(200)"

# STORAGE columns
add_column "STORAGE" "TYPE" "VARCHAR2(200)"
add_column "STORAGE" "CAPACITY_TB" "NUMBER"
add_column "STORAGE" "USED_TB" "NUMBER"
add_column "STORAGE" "PROTOCOL" "VARCHAR2(200)"
add_column "STORAGE" "STATUS" "VARCHAR2(200)"

# FIREWALL columns
add_column "FIREWALL" "MODEL" "VARCHAR2(200)"
add_column "FIREWALL" "FIRMWARE" "VARCHAR2(200)"
add_column "FIREWALL" "RULES_COUNT" "NUMBER"
add_column "FIREWALL" "STATUS" "VARCHAR2(200)"

# SERVICE columns
add_column "SERVICE" "STATUS" "VARCHAR2(200)"
add_column "SERVICE" "SLA" "VARCHAR2(200)"
add_column "SERVICE" "CRITICALITY" "VARCHAR2(200)"
add_column "SERVICE" "URL" "VARCHAR2(200)"

# INCIDENT columns
add_column "INCIDENT" "SEVERITY" "VARCHAR2(200)"
add_column "INCIDENT" "STATUS" "VARCHAR2(200)"
add_column "INCIDENT" "DESCRIPTION" "VARCHAR2(200)"
add_column "INCIDENT" "CREATED_DATE" "DATE"
add_column "INCIDENT" "RESOLVED_DATE" "DATE"

echo ""
echo "--- Phase 3: Adding FK references ---"

# RACK -> LOCATION
add_reference "RACK" "LOCATION"

# NETWORK -> LOCATION, ENVIRONMENT
add_reference "NETWORK" "LOCATION"
add_reference "NETWORK" "ENVIRONMENT"

# APPLICATION -> DEPARTMENT
add_reference "APPLICATION" "DEPARTMENT"

# CONTRACT -> VENDOR, CONTACT
add_reference "CONTRACT" "VENDOR"
add_reference "CONTRACT" "CONTACT"

# PROJECT -> DEPARTMENT, CONTACT
add_reference "PROJECT" "DEPARTMENT"
add_reference "PROJECT" "CONTACT"

# SERVER -> RACK, OPERATING_SYSTEM, ENVIRONMENT, VENDOR
add_reference "SERVER" "RACK"
add_reference "SERVER" "OPERATING_SYSTEM"
add_reference "SERVER" "ENVIRONMENT"
add_reference "SERVER" "VENDOR"

# DATABASE_INSTANCE -> SERVER, ENVIRONMENT
add_reference "DATABASE_INSTANCE" "SERVER"
add_reference "DATABASE_INSTANCE" "ENVIRONMENT"

# STORAGE -> RACK, VENDOR
add_reference "STORAGE" "RACK"
add_reference "STORAGE" "VENDOR"

# FIREWALL -> RACK, VENDOR, LOCATION
add_reference "FIREWALL" "RACK"
add_reference "FIREWALL" "VENDOR"
add_reference "FIREWALL" "LOCATION"

# SERVICE -> APPLICATION, ENVIRONMENT, CONTACT
add_reference "SERVICE" "APPLICATION"
add_reference "SERVICE" "ENVIRONMENT"
add_reference "SERVICE" "CONTACT"

# INCIDENT -> SERVICE, CONTACT
add_reference "INCIDENT" "SERVICE"
add_reference "INCIDENT" "CONTACT"

# JUNCTION: SERVER_APPLICATION -> SERVER, APPLICATION
add_reference "SERVER_APPLICATION" "SERVER"
add_reference "SERVER_APPLICATION" "APPLICATION"

# JUNCTION: SERVER_NETWORK -> SERVER, NETWORK
add_reference "SERVER_NETWORK" "SERVER"
add_reference "SERVER_NETWORK" "NETWORK"

# JUNCTION: APPLICATION_CONTACT -> APPLICATION, CONTACT
add_reference "APPLICATION_CONTACT" "APPLICATION"
add_reference "APPLICATION_CONTACT" "CONTACT"

echo ""
echo "--- Phase 4: Creating records (50 per entity) ---"

# ============ LOCATION (50 records) ============
echo "Creating LOCATION records..."
LOCATIONS=("New York DC1" "London DC1" "Frankfurt DC1" "Tokyo DC1" "Singapore DC1" "Sydney DC1" "Toronto DC1" "Paris DC1" "Amsterdam DC1" "Mumbai DC1" "New York DC2" "London DC2" "Frankfurt DC2" "Tokyo DC2" "Singapore DC2" "Chicago DC1" "Dallas DC1" "Seattle DC1" "Dublin DC1" "Zurich DC1" "Hong Kong DC1" "Seoul DC1" "Sao Paulo DC1" "Dubai DC1" "Stockholm DC1" "Oslo DC1" "Helsinki DC1" "Warsaw DC1" "Prague DC1" "Vienna DC1" "Milan DC1" "Madrid DC1" "Barcelona DC1" "Lisbon DC1" "Berlin DC1" "Munich DC1" "Brussels DC1" "Copenhagen DC1" "Auckland DC1" "Melbourne DC1" "Cape Town DC1" "Montreal DC1" "Vancouver DC1" "Denver DC1" "Atlanta DC1" "Miami DC1" "Phoenix DC1" "Boston DC1" "San Jose DC1" "Portland DC1")
CITIES=("New York" "London" "Frankfurt" "Tokyo" "Singapore" "Sydney" "Toronto" "Paris" "Amsterdam" "Mumbai" "New York" "London" "Frankfurt" "Tokyo" "Singapore" "Chicago" "Dallas" "Seattle" "Dublin" "Zurich" "Hong Kong" "Seoul" "Sao Paulo" "Dubai" "Stockholm" "Oslo" "Helsinki" "Warsaw" "Prague" "Vienna" "Milan" "Madrid" "Barcelona" "Lisbon" "Berlin" "Munich" "Brussels" "Copenhagen" "Auckland" "Melbourne" "Cape Town" "Montreal" "Vancouver" "Denver" "Atlanta" "Miami" "Phoenix" "Boston" "San Jose" "Portland")
COUNTRIES=("US" "UK" "DE" "JP" "SG" "AU" "CA" "FR" "NL" "IN" "US" "UK" "DE" "JP" "SG" "US" "US" "US" "IE" "CH" "HK" "KR" "BR" "AE" "SE" "NO" "FI" "PL" "CZ" "AT" "IT" "ES" "ES" "PT" "DE" "DE" "BE" "DK" "NZ" "AU" "ZA" "CA" "CA" "US" "US" "US" "US" "US" "US" "US")
LOCTYPES=("Data Center" "Data Center" "Data Center" "Data Center" "Data Center" "Data Center" "Data Center" "Data Center" "Data Center" "Data Center" "Data Center" "Data Center" "Data Center" "Data Center" "Data Center" "Data Center" "Data Center" "Data Center" "Data Center" "Data Center" "Data Center" "Data Center" "Data Center" "Data Center" "Data Center" "Data Center" "Data Center" "Data Center" "Data Center" "Data Center" "Office" "Office" "Office" "Office" "Office" "Office" "Office" "Office" "Office" "Office" "Office" "Office" "Office" "Office" "Office" "Office" "Office" "Office" "Office" "Office")
for i in $(seq 0 49); do
    create_record "LOCATION" "NAME=${LOCATIONS[$i]}&ADDRESS=$((100+i)) Main St&CITY=${CITIES[$i]}&COUNTRY=${COUNTRIES[$i]}&TYPE=${LOCTYPES[$i]}"
done
echo "  Done: 50 LOCATION records"

# ============ VENDOR (50 records) ============
echo "Creating VENDOR records..."
VENDORS=("Dell Technologies" "HP Enterprise" "Cisco Systems" "IBM Corporation" "Oracle Corporation" "Microsoft" "VMware" "Red Hat" "Amazon Web Services" "Google Cloud" "Lenovo" "NetApp" "Palo Alto Networks" "Fortinet" "F5 Networks" "Juniper Networks" "Arista Networks" "Pure Storage" "Nutanix" "Commvault" "Splunk" "ServiceNow" "Datadog" "Elastic" "HashiCorp" "Cloudflare" "Akamai" "Zscaler" "CrowdStrike" "SentinelOne" "Broadcom" "Intel" "AMD" "Nvidia" "Samsung" "Western Digital" "Seagate" "Supermicro" "Schneider Electric" "Vertiv" "Eaton" "APC" "Rubrik" "Veeam" "Cohesity" "Atlassian" "GitLab" "JFrog" "Dynatrace" "New Relic")
VTYPES=("Hardware" "Hardware" "Networking" "Services" "Software" "Software" "Software" "Software" "Cloud" "Cloud" "Hardware" "Storage" "Security" "Security" "Networking" "Networking" "Networking" "Storage" "Infrastructure" "Backup" "Monitoring" "ITSM" "Monitoring" "Search" "Infrastructure" "CDN" "CDN" "Security" "Security" "Security" "Hardware" "Hardware" "Hardware" "Hardware" "Hardware" "Storage" "Storage" "Hardware" "Power" "Power" "Power" "Power" "Backup" "Backup" "Backup" "Software" "Software" "Software" "Monitoring" "Monitoring")
for i in $(seq 0 49); do
    create_record "VENDOR" "NAME=${VENDORS[$i]}&WEBSITE=https://www.example.com&PHONE=+1-555-$(printf '%04d' $i)&TYPE=${VTYPES[$i]}&STATUS=Active"
done
echo "  Done: 50 VENDOR records"

# ============ OPERATING_SYSTEM (50 records) ============
echo "Creating OPERATING_SYSTEM records..."
OS_NAMES=("RHEL 9" "RHEL 8" "RHEL 7" "Ubuntu 24.04" "Ubuntu 22.04" "Ubuntu 20.04" "Debian 12" "Debian 11" "CentOS Stream 9" "CentOS 7" "SUSE 15 SP5" "SUSE 15 SP4" "Oracle Linux 9" "Oracle Linux 8" "Rocky Linux 9" "AlmaLinux 9" "Windows Server 2025" "Windows Server 2022" "Windows Server 2019" "Windows Server 2016" "Windows 11" "Windows 10" "macOS Sonoma" "macOS Ventura" "FreeBSD 14" "FreeBSD 13" "AIX 7.3" "AIX 7.2" "Solaris 11.4" "HP-UX 11i v3" "VMware ESXi 8" "VMware ESXi 7" "Proxmox VE 8" "Amazon Linux 2023" "Amazon Linux 2" "Fedora 40" "Fedora 39" "Arch Linux" "openSUSE Leap 15.6" "Alpine Linux 3.20" "CoreOS" "Photon OS 5" "CBL-Mariner 2" "Flatcar Linux" "NixOS 24.05" "Gentoo" "Void Linux" "Clear Linux" "RancherOS" "ChromeOS Flex")
OS_VERS=("9.4" "8.10" "7.9" "24.04" "22.04" "20.04" "12.7" "11.11" "9" "7" "15.5" "15.4" "9.4" "8.10" "9.4" "9.4" "2025" "2022" "2019" "2016" "23H2" "22H2" "14.6" "13.6" "14.1" "13.3" "7.3" "7.2" "11.4" "11iv3" "8.0" "7.0" "8.2" "2023.5" "2" "40" "39" "rolling" "15.6" "3.20" "stable" "5.0" "2.0" "stable" "24.05" "rolling" "rolling" "42000" "1.5" "120")
OS_FAM=("Linux" "Linux" "Linux" "Linux" "Linux" "Linux" "Linux" "Linux" "Linux" "Linux" "Linux" "Linux" "Linux" "Linux" "Linux" "Linux" "Windows" "Windows" "Windows" "Windows" "Windows" "Windows" "macOS" "macOS" "BSD" "BSD" "Unix" "Unix" "Unix" "Unix" "Hypervisor" "Hypervisor" "Hypervisor" "Linux" "Linux" "Linux" "Linux" "Linux" "Linux" "Linux" "Linux" "Linux" "Linux" "Linux" "Linux" "Linux" "Linux" "Linux" "Linux" "ChromeOS")
OS_ARCH=("x86_64" "x86_64" "x86_64" "x86_64" "x86_64" "x86_64" "x86_64" "x86_64" "x86_64" "x86_64" "x86_64" "x86_64" "x86_64" "x86_64" "x86_64" "x86_64" "x86_64" "x86_64" "x86_64" "x86_64" "x86_64" "x86_64" "arm64" "arm64" "x86_64" "x86_64" "ppc64" "ppc64" "sparc64" "ia64" "x86_64" "x86_64" "x86_64" "x86_64" "x86_64" "x86_64" "x86_64" "x86_64" "x86_64" "x86_64" "x86_64" "x86_64" "x86_64" "x86_64" "x86_64" "x86_64" "x86_64" "x86_64" "x86_64" "x86_64")
for i in $(seq 0 49); do
    create_record "OPERATING_SYSTEM" "NAME=${OS_NAMES[$i]}&VERSION=${OS_VERS[$i]}&FAMILY=${OS_FAM[$i]}&ARCH=${OS_ARCH[$i]}"
done
echo "  Done: 50 OPERATING_SYSTEM records"

# ============ ENVIRONMENT (50 records) ============
echo "Creating ENVIRONMENT records..."
ENV_NAMES=("Production" "Staging" "Development" "Testing" "QA" "UAT" "Pre-Production" "DR" "Sandbox" "Demo" "Integration" "Performance" "Security" "Training" "Research" "CI/CD" "Hotfix" "Canary" "Blue" "Green" "Alpha" "Beta" "Gamma" "Load Test" "Stress Test" "Pen Test" "Compliance" "Audit" "Archive" "Legacy" "Migration" "POC" "Lab" "Edge-US" "Edge-EU" "Edge-APAC" "CDN-Primary" "CDN-Secondary" "IoT" "ML Training" "ML Inference" "Data Lake" "Analytics" "Monitoring" "Logging" "Backup" "Recovery" "Staging-EU" "Staging-APAC" "Production-DR")
ENV_TIERS=("Tier 1" "Tier 2" "Tier 3" "Tier 3" "Tier 2" "Tier 2" "Tier 1" "Tier 1" "Tier 4" "Tier 4" "Tier 3" "Tier 2" "Tier 2" "Tier 4" "Tier 4" "Tier 3" "Tier 2" "Tier 1" "Tier 1" "Tier 1" "Tier 4" "Tier 3" "Tier 3" "Tier 3" "Tier 3" "Tier 2" "Tier 2" "Tier 2" "Tier 4" "Tier 4" "Tier 3" "Tier 4" "Tier 4" "Tier 2" "Tier 2" "Tier 2" "Tier 1" "Tier 2" "Tier 3" "Tier 3" "Tier 2" "Tier 2" "Tier 2" "Tier 2" "Tier 3" "Tier 2" "Tier 2" "Tier 2" "Tier 2" "Tier 1")
for i in $(seq 0 49); do
    DESC="Environment for ${ENV_NAMES[$i]} workloads"
    create_record "ENVIRONMENT" "NAME=${ENV_NAMES[$i]}&TIER=${ENV_TIERS[$i]}&DESCRIPTION=$DESC"
done
echo "  Done: 50 ENVIRONMENT records"

# ============ DEPARTMENT (50 records) ============
echo "Creating DEPARTMENT records..."
DEPT_NAMES=("Engineering" "Operations" "Security" "Infrastructure" "DevOps" "QA" "Product" "Design" "Data Science" "Machine Learning" "Finance" "HR" "Legal" "Marketing" "Sales" "Support" "R&D" "Architecture" "Platform" "SRE" "Network Ops" "Database Admin" "Cloud Engineering" "Mobile Dev" "Frontend Dev" "Backend Dev" "Full Stack" "Analytics" "Business Intel" "Compliance" "Risk Management" "Procurement" "Facilities" "Executive" "Project Mgmt" "Change Mgmt" "Release Mgmt" "Incident Mgmt" "Problem Mgmt" "Service Desk" "Identity Mgmt" "Capacity Mgmt" "Availability Mgmt" "Performance Eng" "Test Automation" "Documentation" "Training" "Consulting" "Partner Mgmt" "Customer Success")
DEPT_CODES=("ENG" "OPS" "SEC" "INF" "DEV" "QAT" "PRD" "DES" "DSC" "MLE" "FIN" "HRM" "LEG" "MKT" "SAL" "SUP" "RND" "ARC" "PLT" "SRE" "NOP" "DBA" "CLD" "MOB" "FED" "BED" "FST" "ANL" "BIN" "CMP" "RSK" "PRC" "FAC" "EXE" "PMO" "CHG" "REL" "INC" "PRB" "SDK" "IDM" "CAP" "AVL" "PER" "TAU" "DOC" "TRN" "CON" "PTR" "CSM")
for i in $(seq 0 49); do
    create_record "DEPARTMENT" "NAME=${DEPT_NAMES[$i]}&CODE=${DEPT_CODES[$i]}&MANAGER=Manager $((i+1))&COST_CENTER=CC-$(printf '%04d' $((1000+i)))"
done
echo "  Done: 50 DEPARTMENT records"

# ============ CONTACT (50 records) ============
echo "Creating CONTACT records..."
CONTACT_NAMES=("John Smith" "Jane Doe" "Bob Johnson" "Alice Williams" "Charlie Brown" "Diana Prince" "Edward Norton" "Fiona Apple" "George Lucas" "Helen Troy" "Ivan Petrov" "Julia Roberts" "Kevin Hart" "Laura Palmer" "Mike Chen" "Nina Simone" "Oscar Wilde" "Patricia Moore" "Quinn Hughes" "Rachel Green" "Sam Wilson" "Tina Turner" "Ulrich Stern" "Victoria Beck" "Walter White" "Xena Warrior" "Yuri Gagarin" "Zara Ali" "Adam West" "Betty Ross" "Carl Sagan" "Donna Troy" "Eric Clapton" "Freya Nord" "Grant Ward" "Hanna Lee" "Ian Malcolm" "Janet Van" "Karl Urban" "Lisa Su" "Mark Twain" "Nancy Drew" "Otto Hahn" "Petra Kelly" "Roy Kent" "Susan Storm" "Tom Hardy" "Uma Patel" "Vera Wang" "Will Turner")
ROLES=("SysAdmin" "DevOps" "Security Analyst" "DBA" "Network Engineer" "Cloud Architect" "Team Lead" "Director" "VP Engineering" "CTO" "Developer" "QA Engineer" "Scrum Master" "Product Owner" "Architect" "SysAdmin" "DevOps" "Security Analyst" "DBA" "Network Engineer" "Cloud Architect" "Team Lead" "Director" "Manager" "Engineer" "SysAdmin" "DevOps" "Security Analyst" "DBA" "Network Engineer" "Cloud Architect" "Team Lead" "Director" "Manager" "Engineer" "SysAdmin" "DevOps" "Security Analyst" "DBA" "Network Engineer" "Cloud Architect" "Team Lead" "Director" "Manager" "Engineer" "SysAdmin" "DevOps" "Security Analyst" "DBA" "Network Engineer")
for i in $(seq 0 49); do
    FNAME=$(echo "${CONTACT_NAMES[$i]}" | awk '{print tolower($1)}')
    LNAME=$(echo "${CONTACT_NAMES[$i]}" | awk '{print tolower($2)}')
    create_record "CONTACT" "NAME=${CONTACT_NAMES[$i]}&EMAIL=${FNAME}.${LNAME}@company.com&PHONE=+1-555-$(printf '%04d' $((1000+i)))&ROLE=${ROLES[$i]}&STATUS=Active"
done
echo "  Done: 50 CONTACT records"

# ============ RACK (50 records) ============
echo "Creating RACK records..."
for i in $(seq 1 50); do
    ROW=$(( (i-1) / 10 + 1 ))
    COL=$(( (i-1) % 10 + 1 ))
    LOC_ID=$(( (i-1) % 15 + 1 ))
    CAP=$(( 20 + (i % 30) ))
    POWER=$(( 5 + (i % 15) ))
    STAT="Active"
    if [ $((i % 10)) -eq 0 ]; then STAT="Maintenance"; fi
    create_record "RACK" "NAME=RACK-R${ROW}C${COL}&CAPACITY=${CAP}&POWER_KW=${POWER}&STATUS=${STAT}&LOCATION_ID=${LOC_ID}"
done
echo "  Done: 50 RACK records"

# ============ NETWORK (50 records) ============
echo "Creating NETWORK records..."
for i in $(seq 1 50); do
    OCT2=$(( 168 + (i-1) / 25 ))
    OCT3=$(( (i-1) % 256 ))
    VLAN=$(( 100 + i ))
    LOC_ID=$(( (i-1) % 15 + 1 ))
    ENV_ID=$(( (i-1) % 5 + 1 ))
    if [ $((i % 3)) -eq 0 ]; then NTYPE="Management"; elif [ $((i % 3)) -eq 1 ]; then NTYPE="Production"; else NTYPE="Storage"; fi
    create_record "NETWORK" "NAME=VLAN-${VLAN}&CIDR=10.${OCT2}.${OCT3}.0/24&VLAN=${VLAN}&TYPE=${NTYPE}&LOCATION_ID=${LOC_ID}&ENVIRONMENT_ID=${ENV_ID}"
done
echo "  Done: 50 NETWORK records"

# ============ APPLICATION (50 records) ============
echo "Creating APPLICATION records..."
APP_NAMES=("ERP System" "CRM Platform" "HR Portal" "Finance App" "Inventory Mgmt" "Order Processing" "Email Server" "Web Portal" "API Gateway" "Auth Service" "Payment Gateway" "Reporting Engine" "Data Warehouse" "ETL Pipeline" "Message Queue" "Cache Service" "Search Engine" "Log Aggregator" "Monitoring Dashboard" "CI/CD Pipeline" "Wiki System" "Issue Tracker" "Chat Platform" "Video Conferencing" "File Storage" "Backup System" "DNS Service" "Load Balancer" "CDN Manager" "SSL Manager" "Config Server" "Service Registry" "Secret Vault" "Container Registry" "Artifact Store" "Code Repository" "Build Server" "Test Runner" "Deploy Manager" "Feature Flags" "A/B Testing" "Analytics Engine" "ML Platform" "Data Lake" "Stream Processor" "Batch Scheduler" "Notification Svc" "Audit Logger" "Compliance Tool" "License Manager")
CRIT=("Critical" "Critical" "High" "High" "High" "Critical" "Critical" "High" "Critical" "Critical" "Critical" "Medium" "High" "Medium" "High" "High" "Medium" "Medium" "High" "High" "Low" "Medium" "Medium" "Medium" "Medium" "High" "Critical" "Critical" "High" "High" "High" "High" "Critical" "Medium" "Medium" "High" "Medium" "Medium" "High" "Low" "Low" "Medium" "Medium" "Medium" "Medium" "Medium" "Medium" "High" "High" "Low")
for i in $(seq 0 49); do
    DEPT_ID=$(( i % 20 + 1 ))
    VER="$((i%5+1)).$((i%10)).$((i%3))"
    STAT="Active"
    if [ $((i % 8)) -eq 0 ]; then STAT="Deprecated"; fi
    create_record "APPLICATION" "NAME=${APP_NAMES[$i]}&VERSION=${VER}&STATUS=${STAT}&CRITICALITY=${CRIT[$i]}&URL=https://app$((i+1)).internal.com&DEPARTMENT_ID=${DEPT_ID}"
done
echo "  Done: 50 APPLICATION records"

# ============ CONTRACT (50 records) ============
echo "Creating CONTRACT records..."
for i in $(seq 1 50); do
    VENDOR_ID=$(( (i-1) % 50 + 1 ))
    CONTACT_ID=$(( (i-1) % 50 + 1 ))
    VALUE=$(( 10000 + i * 5000 ))
    STAT="Active"
    if [ $((i % 7)) -eq 0 ]; then STAT="Expired"; fi
    if [ $((i % 11)) -eq 0 ]; then STAT="Pending Renewal"; fi
    CTYPE="Support"
    if [ $((i % 3)) -eq 0 ]; then CTYPE="License"; fi
    if [ $((i % 5)) -eq 0 ]; then CTYPE="Maintenance"; fi
    create_record "CONTRACT" "NAME=CTR-$(printf '%04d' $i)&START_DATE=2024-01-01&END_DATE=2026-12-31&VALUE=${VALUE}&STATUS=${STAT}&TYPE=${CTYPE}&VENDOR_ID=${VENDOR_ID}&CONTACT_ID=${CONTACT_ID}"
done
echo "  Done: 50 CONTRACT records"

# ============ PROJECT (50 records) ============
echo "Creating PROJECT records..."
PRJ_NAMES=("Cloud Migration" "DC Consolidation" "Network Refresh" "Security Hardening" "DR Implementation" "Monitoring Upgrade" "Storage Expansion" "Server Refresh" "App Modernization" "DevOps Transformation" "Zero Trust Network" "Database Migration" "Containerization" "API Platform" "ITSM Implementation" "Compliance Automation" "Capacity Planning" "Cost Optimization" "Performance Tuning" "Disaster Recovery" "Edge Computing" "5G Integration" "AI/ML Platform" "Data Governance" "Identity Federation" "Microservices" "Serverless Migration" "Multi-Cloud" "Hybrid Cloud" "Private Cloud" "Public Cloud" "Green IT" "Automation Initiative" "Self-Service Portal" "ChatOps" "GitOps" "Infrastructure as Code" "Policy as Code" "Observability" "Service Mesh" "Event-Driven Arch" "Data Mesh" "Platform Engineering" "Developer Portal" "Internal Tools" "SRE Adoption" "Chaos Engineering" "FinOps" "SecOps" "MLOps")
for i in $(seq 0 49); do
    DEPT_ID=$(( i % 20 + 1 ))
    CONTACT_ID=$(( i % 50 + 1 ))
    BUDGET=$(( 50000 + i * 10000 ))
    STAT="In Progress"
    if [ $((i % 5)) -eq 0 ]; then STAT="Completed"; fi
    if [ $((i % 7)) -eq 0 ]; then STAT="Planning"; fi
    create_record "PROJECT" "NAME=${PRJ_NAMES[$i]}&STATUS=${STAT}&START_DATE=2025-01-01&END_DATE=2026-06-30&BUDGET=${BUDGET}&DEPARTMENT_ID=${DEPT_ID}&CONTACT_ID=${CONTACT_ID}"
done
echo "  Done: 50 PROJECT records"

# ============ SERVER (50 records) ============
echo "Creating SERVER records..."
for i in $(seq 1 50); do
    RACK_ID=$(( (i-1) % 50 + 1 ))
    OS_ID=$(( (i-1) % 20 + 1 ))
    ENV_ID=$(( (i-1) % 5 + 1 ))
    VENDOR_ID=$(( (i-1) % 3 + 1 ))
    CPU=$(( 4 * (1 + i % 16) ))
    RAM=$(( 16 * (1 + i % 8) ))
    STAT="Running"
    if [ $((i % 10)) -eq 0 ]; then STAT="Maintenance"; fi
    if [ $((i % 15)) -eq 0 ]; then STAT="Decommissioned"; fi
    STYPE="Virtual"
    if [ $((i % 3)) -eq 0 ]; then STYPE="Physical"; fi
    create_record "SERVER" "NAME=SRV-$(printf '%04d' $i)&HOSTNAME=srv$(printf '%04d' $i).internal.com&IP_ADDRESS=10.1.$((i/256)).$((i%256))&CPU_CORES=${CPU}&RAM_GB=${RAM}&STATUS=${STAT}&TYPE=${STYPE}&RACK_ID=${RACK_ID}&OPERATING_SYSTEM_ID=${OS_ID}&ENVIRONMENT_ID=${ENV_ID}&VENDOR_ID=${VENDOR_ID}"
done
echo "  Done: 50 SERVER records"

# ============ DATABASE_INSTANCE (50 records) ============
echo "Creating DATABASE_INSTANCE records..."
DB_ENGINES=("Oracle" "PostgreSQL" "MySQL" "SQL Server" "MongoDB" "Redis" "Cassandra" "MariaDB" "DynamoDB" "CockroachDB")
for i in $(seq 1 50); do
    SERVER_ID=$(( (i-1) % 50 + 1 ))
    ENV_ID=$(( (i-1) % 5 + 1 ))
    ENGINE=${DB_ENGINES[$(( (i-1) % 10 ))]}
    PORT=$((5432 + i % 20))
    SIZE=$(( 50 + i * 20 ))
    STAT="Running"
    if [ $((i % 12)) -eq 0 ]; then STAT="Stopped"; fi
    create_record "DATABASE_INSTANCE" "NAME=DB-$(printf '%04d' $i)&ENGINE=${ENGINE}&VERSION=$((i%5+10)).0&PORT=${PORT}&SIZE_GB=${SIZE}&STATUS=${STAT}&SERVER_ID=${SERVER_ID}&ENVIRONMENT_ID=${ENV_ID}"
done
echo "  Done: 50 DATABASE_INSTANCE records"

# ============ STORAGE (50 records) ============
echo "Creating STORAGE records..."
for i in $(seq 1 50); do
    RACK_ID=$(( (i-1) % 50 + 1 ))
    VENDOR_ID=$(( 12 + (i-1) % 5 ))
    CAP=$(( 10 + i * 5 ))
    USED=$(( CAP * 60 / 100 ))
    STYPE="SAN"
    if [ $((i % 3)) -eq 0 ]; then STYPE="NAS"; fi
    if [ $((i % 5)) -eq 0 ]; then STYPE="Object"; fi
    PROTO="iSCSI"
    if [ $((i % 3)) -eq 0 ]; then PROTO="NFS"; fi
    if [ $((i % 4)) -eq 0 ]; then PROTO="FC"; fi
    STAT="Online"
    if [ $((i % 10)) -eq 0 ]; then STAT="Degraded"; fi
    create_record "STORAGE" "NAME=STG-$(printf '%04d' $i)&TYPE=${STYPE}&CAPACITY_TB=${CAP}&USED_TB=${USED}&PROTOCOL=${PROTO}&STATUS=${STAT}&RACK_ID=${RACK_ID}&VENDOR_ID=${VENDOR_ID}"
done
echo "  Done: 50 STORAGE records"

# ============ FIREWALL (50 records) ============
echo "Creating FIREWALL records..."
FW_MODELS=("PA-5260" "PA-3260" "FortiGate 600F" "FortiGate 200F" "ASA 5585" "ASA 5545" "SRX5800" "SRX4600" "NSa 6700" "NSa 4700")
for i in $(seq 1 50); do
    RACK_ID=$(( (i-1) % 50 + 1 ))
    VENDOR_ID=$(( 13 + (i-1) % 3 ))
    LOC_ID=$(( (i-1) % 15 + 1 ))
    MODEL=${FW_MODELS[$(( (i-1) % 10 ))]}
    RULES=$(( 100 + i * 10 ))
    STAT="Active"
    if [ $((i % 8)) -eq 0 ]; then STAT="Standby"; fi
    create_record "FIREWALL" "NAME=FW-$(printf '%04d' $i)&MODEL=${MODEL}&FIRMWARE=v$((i%5+8)).$((i%10)).0&RULES_COUNT=${RULES}&STATUS=${STAT}&RACK_ID=${RACK_ID}&VENDOR_ID=${VENDOR_ID}&LOCATION_ID=${LOC_ID}"
done
echo "  Done: 50 FIREWALL records"

# ============ SERVICE (50 records) ============
echo "Creating SERVICE records..."
SVC_NAMES=("User Authentication" "Order Management" "Payment Processing" "Email Delivery" "Push Notifications" "Report Generation" "Data Sync" "File Upload" "Image Processing" "Search Indexing" "Cache Management" "Session Management" "Rate Limiting" "API Management" "Log Collection" "Metric Aggregation" "Alert Management" "Incident Response" "Change Management" "Asset Discovery" "Config Management" "Patch Management" "Backup Execution" "DR Orchestration" "DNS Resolution" "Load Distribution" "SSL Termination" "Content Delivery" "WAF Protection" "DDoS Mitigation" "Identity Provider" "Token Service" "Audit Logging" "Compliance Scanning" "Vulnerability Scan" "Penetration Testing" "Container Orchestration" "Service Discovery" "Secret Management" "Certificate Mgmt" "Package Registry" "Binary Storage" "CI Runner" "CD Pipeline" "Feature Management" "Experiment Platform" "ML Model Serving" "Data Pipeline" "Stream Processing" "Job Scheduling")
SLA_VALS=("99.99%" "99.95%" "99.9%" "99.5%" "99%")
for i in $(seq 0 49); do
    APP_ID=$(( i % 50 + 1 ))
    ENV_ID=$(( i % 5 + 1 ))
    CONTACT_ID=$(( i % 50 + 1 ))
    SLA=${SLA_VALS[$(( i % 5 ))]}
    STAT="Operational"
    if [ $((i % 9)) -eq 0 ]; then STAT="Degraded"; fi
    if [ $((i % 13)) -eq 0 ]; then STAT="Down"; fi
    CRIT="Medium"
    if [ $((i % 3)) -eq 0 ]; then CRIT="Critical"; fi
    if [ $((i % 4)) -eq 0 ]; then CRIT="High"; fi
    create_record "SERVICE" "NAME=${SVC_NAMES[$i]}&STATUS=${STAT}&SLA=${SLA}&CRITICALITY=${CRIT}&URL=https://svc$((i+1)).internal.com&APPLICATION_ID=${APP_ID}&ENVIRONMENT_ID=${ENV_ID}&CONTACT_ID=${CONTACT_ID}"
done
echo "  Done: 50 SERVICE records"

# ============ INCIDENT (50 records) ============
echo "Creating INCIDENT records..."
INC_NAMES=("Server Outage" "Network Latency" "DB Connection Pool" "Memory Leak" "Disk Full" "Certificate Expired" "DNS Failure" "Load Balancer Down" "API Timeout" "Auth Failure" "Data Corruption" "Backup Failure" "Replication Lag" "CPU Spike" "Storage IOPS" "Network Partition" "Firewall Block" "DDoS Attack" "Security Breach" "Config Drift" "Deployment Failure" "Pipeline Stuck" "Container Crash" "Pod Eviction" "Node Drain" "Cluster Upgrade" "Version Mismatch" "Dependency Fail" "Rate Limit Hit" "Quota Exceeded" "Permission Denied" "Token Expired" "Session Lost" "Cache Stampede" "Queue Backlog" "Mail Bounce" "Webhook Failure" "Integration Down" "Vendor Outage" "Region Failover" "Scaling Issue" "Cold Start" "Timeout Error" "Connection Reset" "SSL Handshake" "DNS Propagation" "BGP Route Leak" "Power Fluctuation" "Cooling Failure" "Hardware Fault")
SEV_VALS=("Critical" "High" "Medium" "Low")
INC_STATS=("Open" "In Progress" "Resolved" "Closed")
for i in $(seq 0 49); do
    SVC_ID=$(( i % 50 + 1 ))
    CONTACT_ID=$(( i % 50 + 1 ))
    SEV=${SEV_VALS[$(( i % 4 ))]}
    STAT=${INC_STATS[$(( i % 4 ))]}
    create_record "INCIDENT" "NAME=INC-$(printf '%05d' $((i+1)))&SEVERITY=${SEV}&STATUS=${STAT}&DESCRIPTION=${INC_NAMES[$i]} detected in production&CREATED_DATE=2026-01-$((i%28+1))&SERVICE_ID=${SVC_ID}&CONTACT_ID=${CONTACT_ID}"
done
echo "  Done: 50 INCIDENT records"

# ============ JUNCTION: SERVER_APPLICATION (50 records) ============
echo "Creating SERVER_APPLICATION records..."
for i in $(seq 1 50); do
    SRV_ID=$(( (i-1) % 50 + 1 ))
    APP_ID=$(( (i * 7 - 1) % 50 + 1 ))
    create_record "SERVER_APPLICATION" "NAME=SRV${SRV_ID}-APP${APP_ID}&SERVER_ID=${SRV_ID}&APPLICATION_ID=${APP_ID}"
done
echo "  Done: 50 SERVER_APPLICATION records"

# ============ JUNCTION: SERVER_NETWORK (50 records) ============
echo "Creating SERVER_NETWORK records..."
for i in $(seq 1 50); do
    SRV_ID=$(( (i-1) % 50 + 1 ))
    NET_ID=$(( (i * 3 - 1) % 50 + 1 ))
    create_record "SERVER_NETWORK" "NAME=SRV${SRV_ID}-NET${NET_ID}&SERVER_ID=${SRV_ID}&NETWORK_ID=${NET_ID}"
done
echo "  Done: 50 SERVER_NETWORK records"

# ============ JUNCTION: APPLICATION_CONTACT (50 records) ============
echo "Creating APPLICATION_CONTACT records..."
for i in $(seq 1 50); do
    APP_ID=$(( (i-1) % 50 + 1 ))
    CON_ID=$(( (i * 11 - 1) % 50 + 1 ))
    create_record "APPLICATION_CONTACT" "NAME=APP${APP_ID}-CON${CON_ID}&APPLICATION_ID=${APP_ID}&CONTACT_ID=${CON_ID}"
done
echo "  Done: 50 APPLICATION_CONTACT records"

echo ""
echo "========================================="
echo "SEED COMPLETE!"
echo "20 entity types created"
echo "1000 records created (50 per entity)"
echo "========================================="
