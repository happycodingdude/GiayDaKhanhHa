import { useEffect, useState } from 'react'
import { toUserMessage } from '../../../api/errors'
import { Button, Field, Input } from '../../../shared/components/ui'
import { Modal } from '../../../shared/dialogs/Modal'
import { InlineError } from '../../../shared/feedback/QueryState'
import { useToast } from '../../../shared/feedback/ToastProvider'
import { useCreateProductionLine, useUpdateProductionLine } from '../hooks/useProductionLines'
import type { ProductionLineDto } from '../types'

/**
 * Thêm/sửa dây chuyền (CR-001 §7.9). `line` là null khi đang thêm mới.
 *
 * Thứ tự KHÔNG chỉ là thứ tự hiển thị: nó quyết định dây chuyền nào nhận phần dư khi chia đều ở
 * tầng 1 (BR-N17), nên nhãn phải nói rõ điều đó.
 */
export function ProductionLineDialog({
  open,
  line,
  onClose,
}: {
  open: boolean
  line: ProductionLineDto | null
  onClose: () => void
}) {
  const { showToast } = useToast()
  const create = useCreateProductionLine()
  const update = useUpdateProductionLine()

  const [code, setCode] = useState('')
  const [name, setName] = useState('')
  const [sortOrder, setSortOrder] = useState('1')
  const [note, setNote] = useState('')
  const [errors, setErrors] = useState<Record<string, string>>({})

  useEffect(() => {
    if (!open) return

    setCode(line?.code ?? '')
    setName(line?.name ?? '')
    setSortOrder(String(line?.sortOrder ?? 1))
    setNote(line?.note ?? '')
    setErrors({})
    create.reset()
    update.reset()
    // Khởi tạo lại mỗi khi dialog mở cho một dây chuyền khác.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [open, line?.id])

  const mutation = line ? update : create
  const serverError = mutation.isError ? toUserMessage(mutation.error) : null

  const submit = async (event: React.FormEvent) => {
    event.preventDefault()

    const next: Record<string, string> = {}
    if (!code.trim()) next.code = 'Vui lòng nhập mã dây chuyền.'
    else if (code.trim().length > 30) next.code = 'Mã dây chuyền tối đa 30 ký tự.'
    if (!name.trim()) next.name = 'Vui lòng nhập tên dây chuyền.'
    if (!/^\d+$/.test(sortOrder.trim())) next.sortOrder = 'Thứ tự phải là số nguyên không âm.'

    setErrors(next)
    if (Object.keys(next).length > 0) return

    const request = {
      code: code.trim(),
      name: name.trim(),
      sortOrder: Number(sortOrder),
      note: note.trim() || null,
    }

    if (line) {
      await update.mutateAsync({ id: line.id, request })
      showToast(`Đã cập nhật dây chuyền ${request.code}.`)
    } else {
      await create.mutateAsync(request)
      showToast(`Đã thêm dây chuyền ${request.code}.`)
    }

    onClose()
  }

  return (
    <Modal
      open={open}
      title={line ? 'Sửa dây chuyền' : 'Thêm dây chuyền'}
      onClose={onClose}
      width={520}
      footer={
        <>
          <Button onClick={onClose} disabled={mutation.isPending}>
            Huỷ
          </Button>
          <Button type="submit" form="productionLineForm" variant="primary" loading={mutation.isPending}>
            {line ? 'Lưu' : 'Thêm'}
          </Button>
        </>
      }
    >
      <form id="productionLineForm" className="form" onSubmit={submit} noValidate>
        <Field label="Mã dây chuyền" htmlFor="lineCode" required error={errors.code}>
          <Input
            id="lineCode"
            value={code}
            onChange={(event) => setCode(event.target.value)}
            placeholder="DC-01"
            autoFocus
            maxLength={30}
          />
        </Field>

        <Field label="Tên dây chuyền" htmlFor="lineName" required error={errors.name}>
          <Input
            id="lineName"
            value={name}
            onChange={(event) => setName(event.target.value)}
            placeholder="Dây chuyền 1"
            maxLength={100}
          />
        </Field>

        <Field
          label="Thứ tự"
          htmlFor="lineSortOrder"
          error={errors.sortOrder}
          hint="Quyết định thứ tự hiển thị và thứ tự nhận phần dư khi chia đều sản lượng."
        >
          <Input
            id="lineSortOrder"
            inputMode="numeric"
            value={sortOrder}
            onChange={(event) => {
              const value = event.target.value
              if (value !== '' && !/^\d+$/.test(value)) return
              setSortOrder(value)
            }}
          />
        </Field>

        <Field label="Ghi chú" htmlFor="lineNote">
          <Input
            id="lineNote"
            value={note}
            onChange={(event) => setNote(event.target.value)}
            maxLength={500}
            placeholder="Tuỳ chọn"
          />
        </Field>

        {serverError && <InlineError message={serverError} />}
      </form>
    </Modal>
  )
}
