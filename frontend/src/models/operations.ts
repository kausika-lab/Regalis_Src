/** Day 8 — operations modules: housekeeping (M18) and inventory (M19). */

export interface HousekeepingSchedule {
  housekeepingScheduleId: number;
  scheduleNumber: string;
  staffUserId: number;
  staffName: string;
  locationId: number;
  workspaceId: number | null;
  workspaceName: string | null;
  areaName: string | null;
  scheduledDate: string;
  startUtc: string;
  endUtc: string;
  taskType: number; // 1 housekeeping, 2 maintenance, 3 cleaning, 4 repair, 5 inspection
  taskDescription: string;
  notes: string | null;
  status: number; // 1 scheduled, 2 in progress, 3 completed, 4 cancelled, 5 missed
  blocksWorkspace: boolean;
  workspaceBlockId: number | null;
  checkInUtc: string | null;
  checkOutUtc: string | null;
  completionNotes: string | null;
  assignedByUserId: number;
}

export interface SaveHousekeepingPayload {
  staffUserId: number;
  locationId: number;
  workspaceId?: number | null;
  areaName?: string | null;
  startUtc: string;
  endUtc: string;
  taskType: number;
  taskDescription: string;
  notes?: string | null;
  blocksWorkspace: boolean;
}

export const HOUSEKEEPING_TASK_LABELS: Record<number, string> = {
  1: 'Housekeeping',
  2: 'Maintenance',
  3: 'Cleaning',
  4: 'Repair',
  5: 'Inspection',
};

export const HOUSEKEEPING_STATUS_LABELS: Record<number, string> = {
  1: 'Scheduled',
  2: 'In progress',
  3: 'Completed',
  4: 'Cancelled',
  5: 'Missed',
};

export interface InventoryItem {
  inventoryItemId: number;
  itemCode: string;
  itemName: string;
  category: string | null;
  unit: string;
  openingQuantity: number;
  reorderLevel: number;
  unitCost: number | null;
  locationId: number;
  description: string | null;
  currentStock: number;
  needsReorder: boolean;
  isActive: boolean;
}

export interface SaveInventoryItemPayload {
  itemCode: string;
  itemName: string;
  category?: string | null;
  unit: string;
  openingQuantity: number;
  reorderLevel: number;
  unitCost?: number | null;
  locationId: number;
  description?: string | null;
}

export interface InventoryStockRow {
  inventoryItemId: number;
  itemCode: string;
  itemName: string;
  category: string | null;
  unit: string;
  openingQuantity: number;
  totalIn: number;
  totalOut: number;
  totalDamage: number;
  totalAdjustment: number;
  currentStock: number;
  reorderLevel: number;
  needsReorder: boolean;
}

export interface InventoryTransaction {
  inventoryTransactionId: number;
  transactionNumber: string;
  inventoryItemId: number;
  itemCode: string;
  itemName: string;
  unit: string;
  transactionType: number; // 1 in, 2 out, 3 damage, 4 opening, 5 adjustment
  quantity: number;
  transactionDate: string;
  sourceOrDestination: string | null;
  referenceNumber: string | null;
  supplierId: number | null;
  supplierName: string | null;
  purchaseOrderId: number | null;
  poNumber: string | null;
  purchaseInvoiceNumber: string | null;
  attachmentPath: string | null;
  unitCost: number | null;
  damageDescription: string | null;
  damageCause: string | null;
  locationId: number;
  enteredByUserId: number;
  enteredByName: string;
  notes: string | null;
  createdDate: string;
}

export interface CreateInventoryTransactionPayload {
  inventoryItemId: number;
  transactionType: number;
  quantity: number;
  transactionDate?: string | null;
  sourceOrDestination?: string | null;
  referenceNumber?: string | null;
  unitCost?: number | null;
  damageDescription?: string | null;
  damageCause?: string | null;
  notes?: string | null;
  supplierId?: number | null;
  purchaseOrderId?: number | null;
  purchaseInvoiceNumber?: string | null;
  attachmentPath?: string | null;
}

export const INVENTORY_TXN_LABELS: Record<number, string> = {
  1: 'In',
  2: 'Out',
  3: 'Damage',
  4: 'Opening balance',
  5: 'Adjustment',
};
