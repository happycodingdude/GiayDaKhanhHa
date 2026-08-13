import { zodResolver } from '@hookform/resolvers/zod'
import { useNavigate } from '@tanstack/react-router'
import { useForm } from 'react-hook-form'
import { z } from 'zod'
import { toUserMessage } from '../../../api/errors'
import { Button, Field, Input } from '../../../shared/components/ui'
import { InlineError } from '../../../shared/feedback/QueryState'
import { useLogin } from '../hooks/useAuth'

const loginSchema = z.object({
  username: z.string().trim().min(1, 'Vui lòng nhập tên đăng nhập.'),
  password: z.string().min(1, 'Vui lòng nhập mật khẩu.'),
})

type LoginForm = z.infer<typeof loginSchema>

export function LoginPage() {
  const navigate = useNavigate()
  const login = useLogin()

  const {
    register,
    handleSubmit,
    formState: { errors },
  } = useForm<LoginForm>({
    resolver: zodResolver(loginSchema),
    defaultValues: { username: '', password: '' },
  })

  const onSubmit = handleSubmit(async (values) => {
    await login.mutateAsync(values)
    await navigate({ to: '/dashboard' })
  })

  return (
    <div className="login">
      <form className="login__panel" onSubmit={onSubmit} noValidate>
        <div className="login__brand">
          <span className="login__logo" aria-hidden="true">
            👟
          </span>
          <div>
            <h1 className="login__title">Quản lý sản xuất</h1>
            <p className="login__subtitle">Đăng nhập để theo dõi tiến độ đơn hàng</p>
          </div>
        </div>

        <Field label="Tên đăng nhập" htmlFor="username" required error={errors.username?.message}>
          <Input id="username" autoComplete="username" autoFocus {...register('username')} />
        </Field>

        <Field label="Mật khẩu" htmlFor="password" required error={errors.password?.message}>
          <Input id="password" type="password" autoComplete="current-password" {...register('password')} />
        </Field>

        {login.isError && <InlineError message={toUserMessage(login.error)} />}

        <Button type="submit" variant="primary" loading={login.isPending} className="login__submit">
          Đăng nhập
        </Button>
      </form>
    </div>
  )
}
