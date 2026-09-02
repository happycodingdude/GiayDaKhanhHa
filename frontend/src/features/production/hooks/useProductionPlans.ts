import { useQuery } from '@tanstack/react-query'
import { queryKeys } from '../../../app/config/queryKeys'
import { productionApi } from '../api/productionApi'

export function useProductionPlans(orderId: string) {
  return useQuery({
    queryKey: queryKeys.orderProductionPlans(orderId),
    queryFn: ({ signal }) => productionApi.getProductionPlans(orderId, signal),
  })
}
