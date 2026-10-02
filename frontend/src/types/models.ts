export interface Ingredient {
  id: number
  name: string
  quantity: number
  unit: string
  baseCost: number
}
export interface CookingStep {
  id: number
  position: number
  instruction: string
  timerSeconds: number
}
export interface Dish {
  id: number
  name: string
  cuisine: string
  description: string
  image: string
  diet: string
  allergens: string[]
  baseServings: number
  prepMinutes: number
  cookMinutes: number
  effort: string
  ingredients: Ingredient[]
  steps: CookingStep[]
}
export interface Profile {
  displayName: string
  diet: string
  allergens: string[]
  cuisines: string[]
  defaultServings: number
  budget: number
  maxMinutes: number
}
export interface GroceryOption {
  id: string
  name: string
  total: number
  ingredients: { name: string; quantity: number; unit: string; cost: number }[]
}
export interface RestaurantOption {
  id: string
  name: string
  mealPrice: number
  deliveryFee: number
  total: number
  etaMinutes: number
  rating: number
}
export interface Comparison {
  servings: number
  cook: GroceryOption
  order: RestaurantOption
  cookMinutes: number
  effort: string
  moneySavedCooking: number
  minutesSavedOrdering: number
  explanation: string
}
export interface CartLine {
  id: string
  dishId: number
  dishName: string
  servings: number
  kind: 'Cook' | 'Restaurant'
  providerId: string
  providerName: string
  total: number
}
export interface OrderItem {
  dishId: number
  dishName: string
  servings: number
  kind: 'Cook' | 'Restaurant'
  providerName: string
  total: number
}
export interface Order {
  id: string
  createdAt: string
  total: number
  status: string
  items: OrderItem[]
  events: { status: string; createdAt: string }[]
}
export interface Rating {
  id: string
  orderId: string
  stars: number
  comment: string
}
export interface Notice {
  id: string
  orderId: string
  message: string
  createdAt: string
  isRead: boolean
}
